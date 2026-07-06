using System;
using System.Windows.Input;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for HotKeyAssignment: modifier flag mapping, parsing, formatting,
    /// validation, and value equality.
    /// </summary>
    // --------------------------------------------------------------------------
    public class HotKeyAssignmentTests
    {
        [Fact]
        public void Modifiers_None_WhenNoFlagsSet()
        {
            var hk = new HotKeyAssignment();
            Assert.Equal(HotKeyModifiers.None, hk.Modifiers);
        }

        [Fact]
        public void Modifiers_CombinesAllSetFlags()
        {
            var hk = new HotKeyAssignment
            {
                CtrlModifier = true,
                ShiftModifier = true,
                AltModifier = true,
                WinModifier = true,
            };

            var expected = HotKeyModifiers.Control | HotKeyModifiers.Shift
                         | HotKeyModifiers.Alt | HotKeyModifiers.WindowsKey;
            Assert.Equal(expected, hk.Modifiers);
        }

        [Theory]
        [InlineData(Key.LeftShift)]
        [InlineData(Key.RightShift)]
        public void AddModifier_ShiftKeys_SetShiftModifier(Key key)
        {
            var hk = new HotKeyAssignment();
            hk.AddModifier(key);
            Assert.True(hk.ShiftModifier);
            Assert.Equal(Key.None, hk.Letter);
        }

        [Theory]
        [InlineData(Key.LeftCtrl)]
        [InlineData(Key.RightCtrl)]
        public void AddModifier_CtrlKeys_SetCtrlModifier(Key key)
        {
            var hk = new HotKeyAssignment();
            hk.AddModifier(key);
            Assert.True(hk.CtrlModifier);
        }

        [Theory]
        [InlineData(Key.LeftAlt)]
        [InlineData(Key.RightAlt)]
        public void AddModifier_AltKeys_SetAltModifier(Key key)
        {
            var hk = new HotKeyAssignment();
            hk.AddModifier(key);
            Assert.True(hk.AltModifier);
        }

        [Theory]
        [InlineData(Key.LWin)]
        [InlineData(Key.RWin)]
        public void AddModifier_WinKeys_SetWinModifier(Key key)
        {
            var hk = new HotKeyAssignment();
            hk.AddModifier(key);
            Assert.True(hk.WinModifier);
        }

        [Fact]
        public void AddModifier_NonModifierKey_BecomesLetter()
        {
            var hk = new HotKeyAssignment();
            hk.AddModifier(Key.G);
            Assert.Equal(Key.G, hk.Letter);
            Assert.False(hk.CtrlModifier);
            Assert.False(hk.ShiftModifier);
            Assert.False(hk.AltModifier);
            Assert.False(hk.WinModifier);
        }

        [Fact]
        public void ToString_ListsModifiersThenLetter()
        {
            var hk = new HotKeyAssignment { CtrlModifier = true, AltModifier = true, Letter = Key.G };
            Assert.Equal("Ctrl +Alt +G", hk.ToString());
        }

        [Fact]
        public void TextView_MatchesToString()
        {
            var hk = new HotKeyAssignment { WinModifier = true, Letter = Key.L };
            Assert.Equal(hk.ToString(), hk.TextView);
        }

        [Fact]
        public void Validate_Throws_WhenNoLetter()
        {
            var hk = new HotKeyAssignment { CtrlModifier = true, Letter = Key.None };
            Assert.Throws<ApplicationException>(() => hk.Validate());
        }

        [Fact]
        public void Validate_Throws_WhenNoModifier()
        {
            var hk = new HotKeyAssignment { Letter = Key.G };
            Assert.Throws<ApplicationException>(() => hk.Validate());
        }

        [Fact]
        public void Validate_Passes_WhenModifierAndLetterPresent()
        {
            var hk = new HotKeyAssignment { CtrlModifier = true, Letter = Key.G };
            var ex = Record.Exception(() => hk.Validate());
            Assert.Null(ex);
        }

        [Fact]
        public void Equality_TrueForIdenticalAssignments()
        {
            var a = new HotKeyAssignment { CtrlModifier = true, ShiftModifier = true, Letter = Key.G };
            var b = new HotKeyAssignment { CtrlModifier = true, ShiftModifier = true, Letter = Key.G };

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equality_FalseWhenLetterDiffers()
        {
            var a = new HotKeyAssignment { CtrlModifier = true, Letter = Key.G };
            var b = new HotKeyAssignment { CtrlModifier = true, Letter = Key.H };

            Assert.True(a != b);
            Assert.False(a == b);
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void Equality_FalseWhenModifierDiffers()
        {
            var a = new HotKeyAssignment { CtrlModifier = true, Letter = Key.G };
            var b = new HotKeyAssignment { AltModifier = true, Letter = Key.G };
            Assert.True(a != b);
        }

        [Fact]
        public void Equality_BothNull_IsTrue()
        {
            HotKeyAssignment a = null;
            HotKeyAssignment b = null;
            Assert.True(a == b);
        }

        [Fact]
        public void Equality_OneNull_IsFalse()
        {
            var a = new HotKeyAssignment { CtrlModifier = true, Letter = Key.G };
            HotKeyAssignment b = null;
            Assert.True(a != b);
            Assert.False(a.Equals(b));
        }
    }
}
