using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Assignment for hotkey
    /// </summary>
    // --------------------------------------------------------------------------
    public class HotKeyAssignment : BaseModel
    {
        public int Id { get; set; }
        public bool CtrlModifier { get; set; }
        public bool ShiftModifier { get; set; }
        public bool AltModifier { get; set; }
        public bool WinModifier { get; set; }
        public Key Letter { get; set; } = Key.None;

        public HotKeyModifiers Modifiers
        {
            get
            {
                var output = HotKeyModifiers.None;
                if (ShiftModifier) output |= HotKeyModifiers.Shift;
                if (CtrlModifier) output |= HotKeyModifiers.Control;
                if (AltModifier) output |= HotKeyModifiers.Alt;
                if (WinModifier) output |= HotKeyModifiers.WindowsKey;
                return output;
            }
        }


        public string OptionName { get; set; }
        public string TextView => this.ToString();

        public string OptionValue { get; set; }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build up this hotkey with a modifier
        /// </summary>
        // --------------------------------------------------------------------------
        public void AddModifier(Key key)
        {
            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift: ShiftModifier = true; break;
                case Key.LeftCtrl:
                case Key.RightCtrl: CtrlModifier = true; break;
                case Key.LeftAlt:
                case Key.RightAlt: AltModifier = true; break;
                case Key.LWin:
                case Key.RWin: WinModifier = true; break;
                default: Letter = key; break;
            }
            NotifyPropertyChanged(nameof(TextView));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ToString
        /// </summary>
        // --------------------------------------------------------------------------
        public override string ToString()
        {
            var output = new StringBuilder();
            output.Append(CtrlModifier ? "Ctrl +" : "");
            output.Append(ShiftModifier ? "Shift +" : "");
            output.Append(AltModifier ? "Alt +" : "");
            output.Append(WinModifier ? "Win +" : "");
            output.Append(Letter);
            return output.ToString();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Validate
        /// </summary>
        // --------------------------------------------------------------------------
        public void Validate()
        {
            if (Letter == Key.None || Modifiers == HotKeyModifiers.None)
            {
                throw new ApplicationException( "Invalid hotkey.  Must be [ctrl|shift|alt|win] + Letter");
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Equality operators
        /// </summary>
        // --------------------------------------------------------------------------
        public static bool operator ==(HotKeyAssignment obj1, HotKeyAssignment obj2)
        {
            if (((object)obj1) == null && ((object)obj2) == null) return true;
            if (((object)obj1) == null || ((object)obj2) == null) return false;
            return (   obj1.ShiftModifier == obj2.ShiftModifier
                    && obj1.CtrlModifier == obj2.CtrlModifier
                    && obj1.AltModifier == obj2.AltModifier
                    && obj1.WinModifier == obj2.WinModifier
                    && obj1.Letter == obj2.Letter);
        }

        public static bool operator !=(HotKeyAssignment obj1, HotKeyAssignment obj2)
        {
            return !(obj1 == obj2);
        }

    }
}
