using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// For sending mail
    /// </summary>
    // --------------------------------------------------------------------------
    public class QuickMailItem : BaseModel
    {
        string _toAddress;
        public string ToAddress
        {
            get => _toAddress;
            set
            {
                _toAddress = value;
                NotifyPropertyChanged(nameof(ToAddress));
            }
        }

        string _body;
        public string Body
        {
            get => _body;
            set
            {
                _body = value;
                NotifyPropertyChanged(nameof(Body));
            }
        }

        OutlookHelper _outlook;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public QuickMailItem(string toAddress, OutlookHelper outlook)
        {
            _outlook = outlook;
            ToAddress = toAddress;
            Body = "Enter message here.  First line is subject.  Ctrl-S to send.";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Send
        /// </summary>
        // --------------------------------------------------------------------------
        internal void Send()
        {
            var bodyParts = Body.Split(new char[] { '\n' }, 2);

            var subject = bodyParts[0].Trim();
            if (subject == "") subject = "Quick Message From " + Environment.UserName + " on " + Environment.MachineName;
            var body = bodyParts.Length > 1 ? bodyParts[1] : "";

            _outlook.SendMail(ToAddress, subject, body);
        }
    }
}
