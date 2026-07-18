using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for TimerInstance: construction, formatting, the "attention word"
    /// puzzle generation, and the dismiss/delete/promote callbacks.
    /// </summary>
    // --------------------------------------------------------------------------
    public class TimerInstanceTests
    {
        static TimerInstance MakeTimer(
            string description = "Weekly status meeting review",
            string location = "Conference Room A",
            DateTime? endsAt = null,
            DateTime? visibleAt = null)
        {
            var end = endsAt ?? new DateTime(2026, 7, 6, 14, 30, 0);
            var visible = visibleAt ?? new DateTime(2026, 7, 6, 14, 25, 0);
            return new TimerInstance(end, visible, location, description);
        }

        static string[] GetAttentionWords(TimerInstance timer)
        {
            var box = timer.AttentionWords;
            Assert.NotNull(box);
            var type = box.GetType();
            return new[]
            {
                (string)type.GetProperty("Word1").GetValue(box),
                (string)type.GetProperty("Word2").GetValue(box),
                (string)type.GetProperty("Word3").GetValue(box),
            };
        }

        [Fact]
        public void Constructor_SetsCoreProperties()
        {
            var end = new DateTime(2026, 7, 6, 14, 30, 0);
            var visible = new DateTime(2026, 7, 6, 14, 25, 0);
            var timer = new TimerInstance(end, visible, "Room 5", "Design sync");

            Assert.Equal(end, timer.EndsAt);
            Assert.Equal(visible, timer.VisibleTime);
            Assert.Equal("Room 5", timer.Location);
            Assert.Equal("Design sync", timer.Description);
        }

        [Fact]
        public void Constructor_UniqueId_CombinesEndLocationDescription()
        {
            var end = new DateTime(2026, 7, 6, 14, 30, 0);
            var timer = new TimerInstance(end, end, "Room 5", "Design sync");
            Assert.Equal($"{end}|Room 5|Design sync", timer.UniqueId);
        }

        [Fact]
        public void Constructor_CopiesLinks_AndSetsVisibility()
        {
            var links = new[]
            {
                new TimerInstance.LinkDetails { Uri = "https://example.com", Text = "Join" },
            };
            var timer = new TimerInstance(DateTime.Now, DateTime.Now, "loc", "desc", links);

            Assert.Single(timer.Links);
            Assert.Equal("https://example.com", timer.Links[0].Uri);
            Assert.Equal(Visibility.Visible, timer.LinkVisibility);
        }

        [Fact]
        public void LinkVisibility_Collapsed_WhenNoLinks()
        {
            var timer = MakeTimer();
            Assert.Empty(timer.Links);
            Assert.Equal(Visibility.Collapsed, timer.LinkVisibility);
        }

        [Fact]
        public void DecoratedDescription_IsDescriptionPlusDecoration()
        {
            var timer = MakeTimer();
            Assert.Equal(timer.Description + " " + timer.DescriptionDecoration, timer.DecoratedDescription);
        }

        [Fact]
        public void TimeText_FormatsVisibleTimeAsClock()
        {
            var visible = new DateTime(2026, 7, 6, 14, 5, 0);
            var timer = MakeTimer(visibleAt: visible);
            Assert.Equal(visible.ToString(@"h\:mm tt"), timer.TimeText);
        }

        [Fact]
        public void NotificationTimeText_FormatsEndsAtAsClock()
        {
            var end = new DateTime(2026, 7, 6, 9, 0, 0);
            var timer = MakeTimer(endsAt: end);
            Assert.Equal(end.ToString(@"h\:mm tt"), timer.NotificationTimeText);
        }

        [Fact]
        public void SetAttentionWords_ProducesThreeNonEmptyWords()
        {
            var timer = MakeTimer();
            var words = GetAttentionWords(timer);

            Assert.Equal(3, words.Length);
            Assert.All(words, w => Assert.False(string.IsNullOrWhiteSpace(w)));
        }

        [Fact]
        public void SetAttentionWords_IncludesAtLeastOneWordNotInDescription()
        {
            // By design, one of the three attention words is chosen to be absent
            // from the decorated description, so the user must actually read the
            // appointment to pick the odd one out. The model guarantees this against
            // the lowercased decorated text using the word's raw casing (it compares
            // SomeWords entries directly), so we assert the same invariant here -
            // lowercasing the word too would spuriously fail for single-letter
            // entries like "V" whose lowercase letter appears in the text.
            for (var i = 0; i < 50; i++)
            {
                var timer = MakeTimer();
                var words = GetAttentionWords(timer);
                var decorated = timer.DecoratedDescription.ToLower();

                Assert.Contains(words, w => !decorated.Contains(w));
            }
        }

        [Fact]
        public void SetAttentionWords_PadsDecoration_WhenDescriptionHasTooFewWords()
        {
            // "min" is on the ignore list and "of" is under 3 chars, leaving fewer
            // than three usable words, so the generator must pad DescriptionDecoration.
            var timer = MakeTimer(description: "min of");
            Assert.False(string.IsNullOrWhiteSpace(timer.DescriptionDecoration));

            var words = GetAttentionWords(timer);
            Assert.All(words, w => Assert.False(string.IsNullOrWhiteSpace(w)));
        }

        [Fact]
        public void Dismiss_InvokesOnDismiss()
        {
            var timer = MakeTimer();
            var fired = false;
            timer.OnDismiss += () => fired = true;

            timer.Dismiss();
            Assert.True(fired);
        }

        [Fact]
        public void Dismiss_DoesNotThrow_WhenNoHandler()
        {
            var timer = MakeTimer();
            var ex = Record.Exception(() => timer.Dismiss());
            Assert.Null(ex);
        }

        [Fact]
        public void DeleteMe_InvokesOnDeleted()
        {
            var timer = MakeTimer();
            var fired = false;
            timer.OnDeleted = () => fired = true;

            timer.DeleteMe();
            Assert.True(fired);
        }

        [Fact]
        public void PromoteMe_InvokesOnPromote()
        {
            var timer = MakeTimer();
            var fired = false;
            timer.OnPromote = () => fired = true;

            timer.PromoteMe();
            Assert.True(fired);
        }

        [Fact]
        public void Description_Setter_UpdatesDecoratedDescription()
        {
            var timer = MakeTimer();
            timer.Description = "New title";
            Assert.StartsWith("New title", timer.DecoratedDescription);
        }
    }
}
