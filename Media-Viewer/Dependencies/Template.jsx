app.openDocument("{PRPROJ}");

var mediaFile = new File("{FILEPATH}");
app.project.importFiles([mediaFile.fsName]);

var root = app.project.rootItem;
var clip = null;

for (var i = 0; i < root.children.numItems; i++) {
    var item = root.children[i];
    if (item.name === mediaFile.name) {
        clip = item;
        break;
    }
}

//For some reason creating timelines refuses to work properly
//var newSeq = app.project.createNewSequenceFromClips(mediaFile.name + "_Sequence", [importedItem]);
//newSeq.videoTracks[0].insertClip(clip, 0);
//newSeq.audioTracks[0].insertClip(clip, 0);