﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Talisman.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.10.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string Location {
            get {
                return ((string)(this["Location"]));
            }
            set {
                this["Location"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string Calendars {
            get {
                return ((string)(this["Calendars"]));
            }
            set {
                this["Calendars"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("NA")]
        public string CurrentVersion {
            get {
                return ((string)(this["CurrentVersion"]));
            }
            set {
                this["CurrentVersion"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("[]")]
        public string HotKeys {
            get {
                return ((string)(this["HotKeys"]));
            }
            set {
                this["HotKeys"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool CrashedLastTime {
            get {
                return ((bool)(this["CrashedLastTime"]));
            }
            set {
                this["CrashedLastTime"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string CheckForNewVersions {
            get {
                return ((string)(this["CheckForNewVersions"]));
            }
            set {
                this["CheckForNewVersions"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("[\"dialin\\\\.\",\"mysettings\\\\.\",\"\\\\.png$\", \"meetingOptions\\\\/\", \"JoinTeamsMeeting$\"]" +
            "")]
        public string LinkIgnorePatterns {
            get {
                return ((string)(this["LinkIgnorePatterns"]));
            }
            set {
                this["LinkIgnorePatterns"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("[{\"pattern\": \"meetup-join\", \"newName\": \"Join Teams Meeting\"},{\"pattern\": \"bluejea" +
            "ns\", \"newName\": \"Join Bluejeans meeting\"}]")]
        public string LinkRenamePatterns {
            get {
                return ((string)(this["LinkRenamePatterns"]));
            }
            set {
                this["LinkRenamePatterns"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string RecentTimers {
            get {
                return ((string)(this["RecentTimers"]));
            }
            set {
                this["RecentTimers"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string CurrentTimers {
            get {
                return ((string)(this["CurrentTimers"]));
            }
            set {
                this["CurrentTimers"] = value;
            }
        }
    }
}
