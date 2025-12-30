using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

namespace MediaViewer.Extensions
{
    public static class Email
    {

        /// <summary>
        /// Launch an email to a single person.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="Subject"></param>
        /// <param name="Body"></param>
        /// <param name="cc"></param>
        /// <param name="bcc"></param>
        /// <returns></returns>
        public static async Task LaunchAsync(string email, string Subject = "", string Body = "", string cc = "", string bcc = "")
        {
            Subject = Subject.FormatForMailTo();
            Body = Body.FormatForMailTo();

            if (cc != "") { cc = "cc=" + cc + "&"; }
            if (bcc != "") { bcc = "bcc=" + bcc + "&"; }

            await Launcher.LaunchUriAsync(new Uri($"mailto:{email}?{cc}{bcc}subject={Subject}&body={Body}"), new LauncherOptions() { IgnoreAppUriHandlers = true }); ;
        }

        /// <summary>
        /// Launch an email to multiple people.
        /// </summary>
        /// <param name="emails"></param>
        /// <param name="Subject"></param>
        /// <param name="Body"></param>
        /// <param name="ccs"></param>
        /// <param name="bccs"></param>
        /// <returns></returns>
        public static async Task LaunchAsync(List<string> emails, string Subject = "", string Body = "", List<string>? ccs = null, List<string>? bccs = null)
        {
            Subject = Subject.FormatForMailTo();
            Body = Body.FormatForMailTo();

            string email = string.Join(",", emails);

            string cc = "";
            if (ccs != null) { cc = "cc=" + string.Join(",", ccs) + "&"; }

            string bcc = "";
            if (bccs != null) { cc = "bcc=" + string.Join(",", bccs) + "&"; }

            await Launcher.LaunchUriAsync(new Uri($"mailto:{email}?{cc}{bcc}subject={Subject}&body={Body}"), new LauncherOptions() { IgnoreAppUriHandlers = true }); ;
        }

    }

}
