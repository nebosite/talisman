using System.Collections.Generic;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for BaseModel's INotifyPropertyChanged plumbing.
    /// </summary>
    // --------------------------------------------------------------------------
    public class BaseModelTests
    {
        class SampleModel : BaseModel
        {
            public string Alpha { get; set; }
            public int Beta { get; set; }
        }

        [Fact]
        public void NotifyPropertyChanged_RaisesEventWithPropertyName()
        {
            var model = new SampleModel();
            string raised = null;
            model.PropertyChanged += (s, e) => raised = e.PropertyName;

            model.NotifyPropertyChanged(nameof(SampleModel.Alpha));

            Assert.Equal(nameof(SampleModel.Alpha), raised);
        }

        [Fact]
        public void NotifyPropertyChanged_NoSubscriber_DoesNotThrow()
        {
            var model = new SampleModel();
            var ex = Record.Exception(() => model.NotifyPropertyChanged("Alpha"));
            Assert.Null(ex);
        }

        [Fact]
        public void NotifyAllPropertiesChanged_RaisesForEveryPublicProperty()
        {
            var model = new SampleModel();
            var raised = new List<string>();
            model.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            model.NotifyAllPropertiesChanged();

            Assert.Contains(nameof(SampleModel.Alpha), raised);
            Assert.Contains(nameof(SampleModel.Beta), raised);
            Assert.Equal(2, raised.Count);
        }
    }
}
