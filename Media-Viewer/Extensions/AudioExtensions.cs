using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Extras;
using System;
using SoundTouch;
using System.Diagnostics;

namespace MediaViewer.Extensions
{

    /// <summary>
    /// Pitch-preserving playback rate adjustment using SoundTouch algorithm.
    /// Perfect for lectures, podcasts, and audiobooks where natural speech is important.
    /// </summary>
    public class VarispeedSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly SoundTouchProcessor soundTouch;
        private readonly float[] sourceBuffer;
        private readonly float[] soundTouchOutBuffer;
        private int soundTouchSamplesAvailable;
        private float playbackRate = 1.0f;
        private bool sourceExhausted = false;
        private bool hasFlushed = false;

        public VarispeedSampleProvider(ISampleProvider source)
        {
            this.source = source;
            this.WaveFormat = source.WaveFormat;

            // Initialize SoundTouch with settings optimized for speech
            soundTouch = new SoundTouchProcessor();
            soundTouch.SampleRate = (source.WaveFormat.SampleRate);
            soundTouch.Channels = (source.WaveFormat.Channels);

            // Settings optimized for speech clarity
            soundTouch.SetSetting(SettingId.UseQuickSeek, 1);
            soundTouch.SetSetting(SettingId.SequenceDurationMs, 40);
            soundTouch.SetSetting(SettingId.SeekWindowDurationMs, 15);
            soundTouch.SetSetting(SettingId.OverlapDurationMs, 8);

            // Buffers for processing
            sourceBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels];
            soundTouchOutBuffer = new float[source.WaveFormat.SampleRate * source.WaveFormat.Channels * 2];
        }

        public WaveFormat WaveFormat { get; }

        public float PlaybackRate
        {
            get => playbackRate;
            set
            {
                if (value <= 0) throw new ArgumentException("Playback rate must be positive");
                if (Math.Abs(playbackRate - value) > 0.001f)
                {
                    playbackRate = value;
                    soundTouch.Tempo = (value); // Tempo change without pitch shift
                    soundTouch.Clear();
                    soundTouchSamplesAvailable = 0;
                }
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = 0;

            while (samplesRead < count)
            {
                // Get samples from SoundTouch if available
                if (soundTouchSamplesAvailable > 0)
                {
                    int samplesNeeded = Math.Min(count - samplesRead, soundTouchSamplesAvailable);

                    // Ensure we don't exceed buffer bounds
                    if (offset + samplesRead + samplesNeeded > buffer.Length)
                    {
                        samplesNeeded = buffer.Length - offset - samplesRead;
                    }

                    if (samplesNeeded > 0)
                    {
                        // Use Buffer.BlockCopy for same-type arrays or manual copy
                        for (int i = 0; i < samplesNeeded; i++)
                        {
                            buffer[offset + samplesRead + i] = soundTouchOutBuffer[i];
                        }

                        samplesRead += samplesNeeded;
                        soundTouchSamplesAvailable -= samplesNeeded;

                        // Shift remaining samples to beginning of buffer
                        if (soundTouchSamplesAvailable > 0)
                        {
                            for (int i = 0; i < soundTouchSamplesAvailable; i++)
                            {
                                soundTouchOutBuffer[i] = soundTouchOutBuffer[samplesNeeded + i];
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // Need more samples - read from source and process
                if (samplesRead < count && !sourceExhausted)
                {
                    int sourceSamplesRead = source.Read(sourceBuffer, 0, sourceBuffer.Length);

                    if (sourceSamplesRead == 0)
                    {
                        // Mark source as exhausted
                        sourceExhausted = true;

                        // Only flush once
                        if (!hasFlushed)
                        {
                            hasFlushed = true;
                            soundTouch.Flush();

                            // Receive flushed samples - ReceiveSamples expects frame count
                            int maxFrames = soundTouchOutBuffer.Length / WaveFormat.Channels;
                            int receivedFrames = soundTouch.ReceiveSamples(soundTouchOutBuffer, maxFrames);
                            soundTouchSamplesAvailable = receivedFrames * WaveFormat.Channels;
                        }

                        if (soundTouchSamplesAvailable == 0)
                        {
                            break; // No more samples available - exit the loop
                        }
                    }
                    else
                    {
                        // Feed samples to SoundTouch - PutSamples expects frame count
                        int sourceFrames = sourceSamplesRead / WaveFormat.Channels;
                        soundTouch.PutSamples(sourceBuffer, sourceFrames);

                        // Receive processed samples - ReceiveSamples expects frame count
                        int maxFrames = soundTouchOutBuffer.Length / WaveFormat.Channels;
                        int receivedFrames = soundTouch.ReceiveSamples(soundTouchOutBuffer, maxFrames);
                        soundTouchSamplesAvailable = receivedFrames * WaveFormat.Channels;
                    }
                }
                else if (samplesRead < count && sourceExhausted && soundTouchSamplesAvailable == 0)
                {
                    // Source is exhausted and no more samples in SoundTouch buffer
                    break;
                }
            }

            return samplesRead;
        }
    }
    
    
    /// <summary>
    /// Simulates the effect of hearing audio with a slight echo as if you're hearing
    /// the audio alone in an auditorium.
    /// </summary>
    public class ReverbSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly MonoToStereoSampleProvider monoToStereo;
        private readonly bool sourceWasMono;

        // Early reflection delays (simulates room geometry)
        private readonly DelayLine[] earlyReflectionsL;
        private readonly DelayLine[] earlyReflectionsR;

        // Late reverb: Comb filters (parallel)
        private readonly CombFilter[] combFiltersL;
        private readonly CombFilter[] combFiltersR;

        // Series all-pass filters for diffusion
        private readonly AllPassFilter[] allPassL;
        private readonly AllPassFilter[] allPassR;

        // Simple LFO for chorus effect (subtle)
        private float lfoPhase;
        private const float LfoRate = 0.3f; // Slower modulation
        private const float LfoDepth = 0.001f; // Subtle depth

        // High-frequency damping
        private readonly OnePoleLowPass lpL;
        private readonly OnePoleLowPass lpR;

        // Pre-delay for separation
        private readonly float[] preDelayBuffer;
        private int preDelayIndex;

        public WaveFormat WaveFormat { get; }
        public bool EnableEffect { get; set; } = true;
        public float ReverbAmount { get; set; } = 0.5f;
        public float WetMix { get; set; } = 0.25f;

        public ReverbSampleProvider(ISampleProvider source)
        {
            // Check if source is mono and convert to stereo
            if (source.WaveFormat.Channels == 1)
            {
                monoToStereo = new MonoToStereoSampleProvider(source);
                this.source = monoToStereo;
                sourceWasMono = true;
                WaveFormat = monoToStereo.WaveFormat; // Now stereo
            }
            else
            {
                this.source = source;
                sourceWasMono = false;
                WaveFormat = source.WaveFormat;
            }

            int sr = WaveFormat.SampleRate; // Use the potentially converted format

            // Longer pre-delay: 25ms (more spacious)
            preDelayBuffer = new float[(int)(sr * 0.025f)];

            // Early reflections (simulates room boundaries)
            // These create the initial perception of room size
            earlyReflectionsL = new[]
            {
                    new DelayLine((int)(sr * 0.019f), 0.8f),  // 19ms - close wall
                    new DelayLine((int)(sr * 0.031f), 0.6f),  // 31ms - side wall
                    new DelayLine((int)(sr * 0.047f), 0.5f),  // 47ms - far wall
                    new DelayLine((int)(sr * 0.061f), 0.4f),  // 61ms - ceiling
                };

            earlyReflectionsR = new[]
            {
                    new DelayLine((int)(sr * 0.021f), 0.8f),  // 21ms - close wall (slightly different)
                    new DelayLine((int)(sr * 0.033f), 0.6f),  // 33ms - side wall
                    new DelayLine((int)(sr * 0.049f), 0.5f),  // 49ms - far wall
                    new DelayLine((int)(sr * 0.063f), 0.4f),  // 63ms - ceiling
                };

            // Longer comb filters for larger room
            combFiltersL = new[]
            {
                    new CombFilter((int)(sr * 0.0567f)),  // ~57ms
                    new CombFilter((int)(sr * 0.0617f)),  // ~62ms
                    new CombFilter((int)(sr * 0.0673f)),  // ~67ms
                    new CombFilter((int)(sr * 0.0719f)),  // ~72ms
                    new CombFilter((int)(sr * 0.0797f)),  // ~80ms
                };

            combFiltersR = new[]
            {
                    new CombFilter((int)(sr * 0.0583f)),  // ~58ms (detuned)
                    new CombFilter((int)(sr * 0.0631f)),  // ~63ms
                    new CombFilter((int)(sr * 0.0689f)),  // ~69ms
                    new CombFilter((int)(sr * 0.0733f)),  // ~73ms
                    new CombFilter((int)(sr * 0.0811f)),  // ~81ms
                };

            // More all-pass stages for better diffusion
            allPassL = new[]
            {
                    new AllPassFilter((int)(sr * 0.0089f), 0.6f),   // ~9ms
                    new AllPassFilter((int)(sr * 0.0127f), 0.6f),   // ~13ms
                    new AllPassFilter((int)(sr * 0.0199f), 0.5f),   // ~20ms
                };

            allPassR = new[]
            {
                    new AllPassFilter((int)(sr * 0.0091f), 0.6f),   // ~9.1ms
                    new AllPassFilter((int)(sr * 0.0131f), 0.6f),   // ~13.1ms
                    new AllPassFilter((int)(sr * 0.0203f), 0.5f),   // ~20.3ms
                };

            // Higher cutoff for more air (6kHz instead of 4kHz)
            lpL = new OnePoleLowPass(6000, sr);
            lpR = new OnePoleLowPass(6000, sr);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            if (!EnableEffect || ReverbAmount <= 0)
                return samplesRead;

            int channels = WaveFormat.Channels;

            // Should always be stereo now after mono conversion
            // But keep the safety check
            if (channels < 2)
                return samplesRead;

            float feedback = 0.6f + (ReverbAmount * 0.25f);
            float damp = 0.2f;

            for (int i = 0; i < samplesRead; i += channels)
            {
                float inputL = buffer[offset + i];
                float inputR = buffer[offset + i + 1]; // Always exists now

                float mono = (inputL + inputR) * 0.5f;

                // Pre-delay
                float predelayed = preDelayBuffer[preDelayIndex];
                preDelayBuffer[preDelayIndex] = mono;
                preDelayIndex = (preDelayIndex + 1) % preDelayBuffer.Length;

                // Subtle LFO modulation
                float mod = 1f + MathF.Sin(lfoPhase) * LfoDepth;
                lfoPhase += 2f * MathF.PI * LfoRate / WaveFormat.SampleRate;
                if (lfoPhase > MathF.PI * 2f) lfoPhase -= MathF.PI * 2f;

                // Early reflections (room geometry)
                float earlyL = 0f;
                float earlyR = 0f;

                foreach (var early in earlyReflectionsL)
                    earlyL += early.Process(predelayed);

                foreach (var early in earlyReflectionsR)
                    earlyR += early.Process(predelayed);

                earlyL *= 0.25f; // Normalize
                earlyR *= 0.25f;

                // Late reverb: parallel comb filters
                float combOutL = 0f;
                float combOutR = 0f;

                foreach (var comb in combFiltersL)
                {
                    combOutL += comb.Process(predelayed + earlyL * 0.3f, feedback * mod);
                }

                foreach (var comb in combFiltersR)
                {
                    combOutR += comb.Process(predelayed + earlyR * 0.3f, feedback * (2f - mod));
                }

                // Average the parallel combs
                combOutL *= 0.2f; // 5 filters now
                combOutR *= 0.2f;

                // Light high-frequency damping
                combOutL = lpL.Process(combOutL);
                combOutR = lpR.Process(combOutR);

                // Series all-pass diffusion
                float diffusedL = combOutL;
                float diffusedR = combOutR;

                foreach (var ap in allPassL)
                    diffusedL = ap.Process(diffusedL);

                foreach (var ap in allPassR)
                    diffusedR = ap.Process(diffusedR);

                // Add early reflections to diffused signal
                diffusedL += earlyL * 0.4f;
                diffusedR += earlyR * 0.4f;

                // Minimal stereo widening (keeps sound more frontal)
                float mid = (diffusedL + diffusedR) * 0.5f;
                float side = (diffusedL - diffusedR) * 0.3f; // Reduced from 0.5f
                float wetL = mid + side;
                float wetR = mid - side;

                // Apply wet/dry mix
                buffer[offset + i] = (inputL * (1f - WetMix)) + (wetL * WetMix);
                buffer[offset + i + 1] = (inputR * (1f - WetMix)) + (wetR * WetMix);
            }

            return samplesRead;
        }
    }

    /// <summary>
    /// Simulates the effect of hearing audio from an isolated room.
    /// Combines heavy low-pass filtering (muffled sound through walls) with reverb.
    /// Features good sound insulation for a fainter, more distant sound.
    /// Automatically converts mono sources to stereo for full spatial effect.
    /// </summary>
    public class IsolationSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly MonoToStereoSampleProvider monoToStereo;
        private readonly bool sourceWasMono;

        // Aggressive low-pass filters for wall muffling (good insulation)
        private readonly OnePoleLowPass wallFilterL;
        private readonly OnePoleLowPass wallFilterR;
        private readonly OnePoleLowPass wallFilterL2;
        private readonly OnePoleLowPass wallFilterR2;
        private readonly OnePoleLowPass wallFilterL3;  // Third stage for better insulation
        private readonly OnePoleLowPass wallFilterR3;

        // Bathroom reverb (small, highly reflective)
        private readonly CombFilter[] bathroomCombL;
        private readonly CombFilter[] bathroomCombR;
        private readonly AllPassFilter[] bathroomAllPassL;
        private readonly AllPassFilter[] bathroomAllPassR;

        // Bass boost (club music emphasizes bass)
        private float bassAccumL;
        private float bassAccumR;

        public WaveFormat WaveFormat { get; }
        public bool EnableEffect { get; set; } = true;
        public float Intensity { get; set; } = 0.7f; // How much effect to apply (wet/dry mix)
        public float BassBoost { get; set; } = 0.5f; // 0.0 to 1.0 - Bass boost amount

        public IsolationSampleProvider(ISampleProvider source)
        {
            // Check if source is mono and convert to stereo
            if (source.WaveFormat.Channels == 1)
            {
                monoToStereo = new MonoToStereoSampleProvider(source);
                this.source = monoToStereo;
                sourceWasMono = true;
                WaveFormat = monoToStereo.WaveFormat; // Now stereo
            }
            else
            {
                this.source = source;
                sourceWasMono = false;
                WaveFormat = source.WaveFormat;
            }

            int sr = WaveFormat.SampleRate; // Use the potentially converted format

            // Very heavy low-pass at ~250Hz (simulates good sound insulation)
            // Stack three filters for even steeper rolloff
            wallFilterL = new OnePoleLowPass(250, sr);
            wallFilterR = new OnePoleLowPass(250, sr);
            wallFilterL2 = new OnePoleLowPass(250, sr);
            wallFilterR2 = new OnePoleLowPass(250, sr);
            wallFilterL3 = new OnePoleLowPass(200, sr);  // Extra stage at 200Hz
            wallFilterR3 = new OnePoleLowPass(200, sr);

            // Small bathroom reverb (short, bright, reflective)
            bathroomCombL = new[]
            {
            new CombFilter((int)(sr * 0.0297f)),  // ~30ms
            new CombFilter((int)(sr * 0.0371f)),  // ~37ms
            new CombFilter((int)(sr * 0.0411f)),  // ~41ms
        };

            bathroomCombR = new[]
            {
            new CombFilter((int)(sr * 0.0313f)),  // ~31ms (detuned)
            new CombFilter((int)(sr * 0.0379f)),  // ~38ms
            new CombFilter((int)(sr * 0.0423f)),  // ~42ms
        };

            bathroomAllPassL = new[]
            {
            new AllPassFilter((int)(sr * 0.0051f), 0.7f),  // ~5ms
            new AllPassFilter((int)(sr * 0.0089f), 0.7f),  // ~9ms
        };

            bathroomAllPassR = new[]
            {
            new AllPassFilter((int)(sr * 0.0053f), 0.7f),  // ~5.3ms
            new AllPassFilter((int)(sr * 0.0091f), 0.7f),  // ~9.1ms
        };
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            if (!EnableEffect)
                return samplesRead;

            int channels = WaveFormat.Channels;

            // Should always be stereo now after mono conversion
            if (channels < 2)
                return samplesRead;

            float intensity = Math.Clamp(Intensity, 0f, 1f);
            float bassBoost = Math.Clamp(BassBoost, 0f, 1f);

            // Fixed insulation attenuation - always fully closed when effect is enabled
            float insulationAttenuation = 0.35f; // 35% volume (well-insulated)

            // Calculate bass boost multiplier (0.5x to 2.0x based on slider)
            float bassMultiplier = 0.5f + (bassBoost * 1.5f); // 0.5 to 2.0

            for (int i = 0; i < samplesRead; i += channels)
            {
                float inputL = buffer[offset + i];
                float inputR = buffer[offset + i + 1]; // Always exists now

                // Step 1: Muffle through walls (very aggressive low-pass for good insulation)
                float muffledL = wallFilterL.Process(inputL);
                muffledL = wallFilterL2.Process(muffledL);
                muffledL = wallFilterL3.Process(muffledL);  // Third stage
                float muffledR = wallFilterR.Process(inputR);
                muffledR = wallFilterR2.Process(muffledR);
                muffledR = wallFilterR3.Process(muffledR);  // Third stage

                // Apply fixed insulation attenuation (always fully closed)
                muffledL *= insulationAttenuation;
                muffledR *= insulationAttenuation;

                // Step 2: Bass boost (controllable via BassBoost property)
                bassAccumL = (bassAccumL * 0.985f) + (muffledL * 0.015f);
                bassAccumR = (bassAccumR * 0.985f) + (muffledR * 0.015f);
                muffledL += bassAccumL * bassMultiplier;
                muffledR += bassAccumR * bassMultiplier;

                // Step 3: Apply bathroom reverb
                float reverbL = 0f;
                float reverbR = 0f;

                float feedback = 0.68f; // Bright, reflective surfaces
                foreach (var comb in bathroomCombL)
                {
                    reverbL += comb.Process(muffledL, feedback);
                }
                foreach (var comb in bathroomCombR)
                {
                    reverbR += comb.Process(muffledR, feedback);
                }

                reverbL *= 0.33f; // Normalize
                reverbR *= 0.33f;

                // Diffusion
                foreach (var ap in bathroomAllPassL)
                    reverbL = ap.Process(reverbL);
                foreach (var ap in bathroomAllPassR)
                    reverbR = ap.Process(reverbR);

                // Step 4: Mix with muffled signal (fixed ratio for bathroom acoustics)
                float wetMix = 0.75f; // Fixed 75% wet for bathroom reverb
                float dryMix = 1f - wetMix;

                float outputL = (muffledL * dryMix) + (reverbL * wetMix);
                float outputR = (muffledR * dryMix) + (reverbR * wetMix);

                // Apply makeup gain to compensate for filtering losses
                float makeupGain = 1.4f; // Boost the final output
                outputL *= makeupGain;
                outputR *= makeupGain;

                // Soft clipping to prevent distortion
                outputL = Math.Clamp(outputL, -1f, 1f);
                outputR = Math.Clamp(outputR, -1f, 1f);

                // Mix with original based on intensity (controls wet/dry mix of entire effect)
                // Now always write to both channels
                buffer[offset + i] = (inputL * (1f - intensity)) + (outputL * intensity);
                buffer[offset + i + 1] = (inputR * (1f - intensity)) + (outputR * intensity);
            }

            return samplesRead;
        }
    }


    /// <summary>
    /// Controls stereo balance/pan, allowing audio to be shifted from left to right ear.
    /// Automatically converts mono sources to stereo for panning to work.
    /// </summary>
    public class StereoPanSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly MonoToStereoSampleProvider monoToStereo;
        private readonly bool sourceWasMono;

        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Pan position: -1.0 (full left), 0.0 (center), 1.0 (full right)
        /// </summary>
        public float Pan { get; set; } = 0.0f;

        public StereoPanSampleProvider(ISampleProvider source)
        {
            // Check if source is mono
            if (source.WaveFormat.Channels == 1)
            {
                // Convert mono to stereo so panning will work
                monoToStereo = new MonoToStereoSampleProvider(source);
                this.source = monoToStereo;
                sourceWasMono = true;
                WaveFormat = monoToStereo.WaveFormat; // Now stereo
            }
            else
            {
                this.source = source;
                sourceWasMono = false;
                WaveFormat = source.WaveFormat;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            int channels = WaveFormat.Channels;

            // Should always be stereo now due to mono conversion
            if (channels != 2)
            {
                return samplesRead;
            }

            // Clamp pan value to valid range
            float pan = Math.Clamp(Pan, -1f, 1f);

            // Calculate left and right channel gains using constant power panning
            // This ensures perceived volume remains constant as you pan
            float panAngle = (pan + 1f) * 0.5f * MathF.PI * 0.5f; // Map -1..1 to 0..π/2
            float leftGain = MathF.Cos(panAngle);
            float rightGain = MathF.Sin(panAngle);

            for (int i = 0; i < samplesRead; i += channels)
            {
                float left = buffer[offset + i];
                float right = buffer[offset + i + 1];

                // Apply constant power panning
                // When panning left: reduce right channel, keep left channel
                // When panning right: reduce left channel, keep right channel
                buffer[offset + i] = left * leftGain;
                buffer[offset + i + 1] = right * rightGain;
            }

            return samplesRead;
        }
    }


    #region Reverb Building Blocks

    internal class DelayLine
    {
        private readonly float[] buffer;
        private readonly float gain;
        private int index;

        public DelayLine(int size, float gain)
        {
            buffer = new float[size];
            this.gain = gain;
        }

        public float Process(float input)
        {
            float output = buffer[index] * gain;
            buffer[index] = input;
            index = (index + 1) % buffer.Length;
            return output;
        }
    }

    internal class OnePoleLowPass
    {
        private readonly float a;
        private float z;

        public OnePoleLowPass(float cutoff, int sampleRate)
        {
            a = MathF.Exp(-2f * MathF.PI * cutoff / sampleRate);
        }

        public float Process(float input)
        {
            z = (input * (1 - a)) + (z * a);
            return z;
        }
    }

    internal class CombFilter
    {
        private readonly float[] buffer;
        private int index;

        public CombFilter(int size)
        {
            buffer = new float[size];
        }

        public float Process(float input, float feedback)
        {
            float output = buffer[index];
            buffer[index] = input + (output * feedback);
            index = (index + 1) % buffer.Length;
            return output;
        }
    }

    internal class AllPassFilter
    {
        private readonly float[] buffer;
        private readonly float feedback;
        private int index;

        public AllPassFilter(int size, float feedback)
        {
            buffer = new float[size];
            this.feedback = feedback;
        }

        public float Process(float input)
        {
            float buffered = buffer[index];
            float output = -input + buffered;
            buffer[index] = input + (buffered * feedback);
            index = (index + 1) % buffer.Length;
            return output;
        }
    }

    #endregion
}