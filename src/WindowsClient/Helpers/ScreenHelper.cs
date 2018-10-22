using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace Talisman
{
    public static class ScreenExtensions
    {
        public static bool ContainsPoint(this Screen screen, double x, double y)
        {
            return !(x < screen.Bounds.Left
                || x > screen.Bounds.Right
                || y < screen.Bounds.Top
                || y > screen.Bounds.Bottom);
        }
    }
    // --------------------------------------------------------------------------
    /// <summary>
    /// Base model for stuff that can be visualized
    /// </summary>
    // --------------------------------------------------------------------------
    public class ScreenHelper 
    {
        public static Screen MainScreen => Screen.AllScreens.Where(s => s.Primary).First();

        // --------------------------------------------------------------------------
        /// <summary>
        /// If any part of the window is off screen, move it into the screen all the way
        /// </summary>
        // --------------------------------------------------------------------------
        public static void EnsureWindowIsVisible(Window window)
        {
            var source = PresentationSource.FromVisual(window);
            var xCorrection = 1.0 / source.CompositionTarget.TransformToDevice.M11;
            var yCorrection = 1.0 / source.CompositionTarget.TransformToDevice.M22;

            var left = window.Left / xCorrection;
            var right = left + window.Width / xCorrection;
            var top = window.Top / yCorrection;
            var bottom = top + window.Height / yCorrection;


            Screen foundScreen = null;
            // Find the screen that is us
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.ContainsPoint(left, top)
                    || screen.ContainsPoint(right, top)
                    || screen.ContainsPoint(right, bottom)
                    || screen.ContainsPoint(left, bottom))
                {
                    foundScreen = screen;
                    break;
                }
            }

            if (foundScreen == null) foundScreen = MainScreen;

            if (left < foundScreen.Bounds.Left) window.Left = foundScreen.Bounds.Left * xCorrection;
            if (top < foundScreen.Bounds.Top) window.Top = foundScreen.Bounds.Top * yCorrection;
            if (right > foundScreen.Bounds.Right) window.Left = (foundScreen.Bounds.Right - window.Width)*xCorrection;
            if (bottom + window.Height > foundScreen.Bounds.Bottom) window.Top = (foundScreen.Bounds.Bottom - window.Height)*yCorrection;
        }
    }
}
