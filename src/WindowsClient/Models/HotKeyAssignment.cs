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
        public bool CtrlModifier { get; set; }
        public bool ShiftModifier { get; set; }
        public bool AltModifier { get; set; }
        public Key Letter { get; set; } = Key.None;

        public string OptionName { get; set; }
        public string TextView => this.ToString();

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
            if (Letter == Key.None || (!CtrlModifier && !ShiftModifier && !AltModifier))
            {
                throw new ApplicationException( "Invalid hotkey.  Must be [ctrl|shift|alt] + Letter");
            }
        }
    }
}
