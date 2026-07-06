using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for QuickMailItem construction/default state. The Send() path is
    /// intentionally not exercised here because it drives live Outlook interop
    /// through OutlookHelper; splitting that logic behind a seam would let it be
    /// unit tested and is worth doing when Send() next changes.
    /// </summary>
    // --------------------------------------------------------------------------
    public class QuickMailItemTests
    {
        [Fact]
        public void Constructor_SetsToAddress()
        {
            var item = new QuickMailItem("someone@example.com", null);
            Assert.Equal("someone@example.com", item.ToAddress);
        }

        [Fact]
        public void Constructor_SeedsBodyWithInstructions()
        {
            var item = new QuickMailItem("someone@example.com", null);
            Assert.False(string.IsNullOrWhiteSpace(item.Body));
        }

        [Fact]
        public void ToAddress_Setter_RaisesPropertyChanged()
        {
            var item = new QuickMailItem("a@example.com", null);
            string raised = null;
            item.PropertyChanged += (s, e) => raised = e.PropertyName;

            item.ToAddress = "b@example.com";

            Assert.Equal(nameof(QuickMailItem.ToAddress), raised);
            Assert.Equal("b@example.com", item.ToAddress);
        }

        [Fact]
        public void Body_Setter_RaisesPropertyChanged()
        {
            var item = new QuickMailItem("a@example.com", null);
            string raised = null;
            item.PropertyChanged += (s, e) => raised = e.PropertyName;

            item.Body = "hello";

            Assert.Equal(nameof(QuickMailItem.Body), raised);
        }
    }
}
