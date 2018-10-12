﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Data for the notification widget
    /// </summary>
    // --------------------------------------------------------------------------
    public class NotificationData : BaseModel
    {
        public string Message { get; private set; }


        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public NotificationData(string message)
        {
            Message = message;
        }
    }
}
