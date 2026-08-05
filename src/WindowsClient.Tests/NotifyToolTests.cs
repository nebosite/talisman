using Newtonsoft.Json.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the "notify" MCP tool: argument resolution with the Settings
    /// fallback, the default title, the success/error results, and that the
    /// definition reads its default email lazily on each call.
    /// </summary>
    // --------------------------------------------------------------------------
    public class NotifyToolTests
    {
        static JObject Args(string message = "hi", string title = null, string to = null)
        {
            var o = new JObject();
            if (message != null) o["message"] = message;
            if (title != null) o["title"] = title;
            if (to != null) o["to"] = to;
            return o;
        }

        [Fact]
        public void TryResolve_UsesCallArgs()
        {
            var ok = SendResolve(Args(message: "body", title: "Heads up", to: "me@pomail.net"),
                defaultEmail: null, out var to, out var title, out var message, out var error);

            Assert.True(ok, error);
            Assert.Equal("me@pomail.net", to);
            Assert.Equal("Heads up", title);
            Assert.Equal("body", message);
        }

        [Fact]
        public void TryResolve_FallsBackToDefaultEmail_WhenToOmitted()
        {
            NotifyTool.TryResolve(Args(message: "body"),
                defaultEmail: "default@pomail.net", out var to, out _, out _, out var error);

            Assert.Equal("default@pomail.net", to);
        }

        [Fact]
        public void TryResolve_CallToOverridesDefault()
        {
            NotifyTool.TryResolve(Args(message: "body", to: "explicit@pomail.net"),
                defaultEmail: "default@pomail.net", out var to, out _, out _, out _);

            Assert.Equal("explicit@pomail.net", to);
        }

        [Fact]
        public void TryResolve_DefaultsTitleToTalisman()
        {
            NotifyTool.TryResolve(Args(message: "body", to: "me@pomail.net"),
                null, out _, out var title, out _, out _);

            Assert.Equal("Talisman", title);
        }

        [Fact]
        public void TryResolve_MissingMessage_Fails()
        {
            var ok = NotifyTool.TryResolve(Args(message: null, to: "me@pomail.net"),
                null, out _, out _, out _, out var error);

            Assert.False(ok);
            Assert.Contains("message", error);
        }

        [Fact]
        public void TryResolve_NoRecipientAnywhere_Fails()
        {
            var ok = NotifyTool.TryResolve(Args(message: "body"),
                defaultEmail: null, out _, out _, out _, out var error);

            Assert.False(ok);
            Assert.Contains("recipient", error);
        }

        [Fact]
        public void Run_Success_InvokesSendWithToTitleMessage()
        {
            string sentTo = null, sentTitle = null, sentMsg = null;
            var result = NotifyTool.Run(Args(message: "done", title: "Claude", to: "me@pomail.net"),
                null, (to, title, msg) => { sentTo = to; sentTitle = title; sentMsg = msg; });

            Assert.False(result.IsError);
            Assert.Equal("me@pomail.net", sentTo);
            Assert.Equal("Claude", sentTitle);
            Assert.Equal("done", sentMsg);
        }

        [Fact]
        public void Run_ValidationError_DoesNotSend()
        {
            var sent = false;
            var result = NotifyTool.Run(Args(message: "body"),  // no recipient
                null, (t, ti, m) => sent = true);

            Assert.True(result.IsError);
            Assert.False(sent);
        }

        [Fact]
        public void Run_SendThrows_ReturnsError()
        {
            var result = NotifyTool.Run(Args(message: "body", to: "me@pomail.net"),
                null, (t, ti, m) => throw new System.Exception("outlook down"));

            Assert.True(result.IsError);
            Assert.Contains("Failed to send notification", result.Text);
            Assert.Contains("outlook down", result.Text);
        }

        [Fact]
        public void Create_DefinitionExposesSchemaAndReadsDefaultLazily()
        {
            var email = "one@pomail.net";
            string sentTo = null;
            var def = NotifyTool.Create(() => email, (to, title, msg) => sentTo = to);

            Assert.Equal("notify", def.Name);
            Assert.Contains("message", def.InputSchema["required"].Values<string>());

            def.Invoke(Args(message: "a"));
            Assert.Equal("one@pomail.net", sentTo);

            email = "two@pomail.net";
            def.Invoke(Args(message: "b"));
            Assert.Equal("two@pomail.net", sentTo);
        }

        // Local wrapper to keep the first test's call site readable.
        static bool SendResolve(JObject args, string defaultEmail, out string to, out string title, out string message, out string error)
            => NotifyTool.TryResolve(args, defaultEmail, out to, out title, out message, out error);
    }
}
