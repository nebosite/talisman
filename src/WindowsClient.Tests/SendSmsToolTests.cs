using Newtonsoft.Json.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the "sendsms" MCP tool: number normalization, argument resolution
    /// with the Settings fallback, the success/error results, and that the
    /// definition reads its default number lazily on each call.
    /// </summary>
    // --------------------------------------------------------------------------
    public class SendSmsToolTests
    {
        static JObject Args(string message = "hi", string to = null)
        {
            var o = new JObject();
            if (message != null) o["message"] = message;
            if (to != null) o["to"] = to;
            return o;
        }

        [Theory]
        [InlineData("5551234567", "5551234567")]
        [InlineData("(555) 123-4567", "5551234567")]
        [InlineData("555.123.4567", "5551234567")]
        [InlineData("1-555-123-4567", "5551234567")]   // leading country code dropped
        [InlineData("+1 555 123 4567", "5551234567")]
        public void TryNormalizeNumber_StripsPunctuationAndCountryCode(string raw, string expected)
        {
            Assert.True(SendSmsTool.TryNormalizeNumber(raw, out var digits, out _));
            Assert.Equal(expected, digits);
        }

        [Theory]
        [InlineData("55512345")]       // too short
        [InlineData("2345551234567")]  // too long, not a leading 1
        [InlineData("")]
        [InlineData(null)]
        public void TryNormalizeNumber_RejectsBadLength(string raw)
        {
            Assert.False(SendSmsTool.TryNormalizeNumber(raw, out _, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TryResolve_NormalizesTheCallNumber()
        {
            var ok = SendSmsTool.TryResolve(
                Args(message: "yo", to: "425-350-6007"),
                defaultNumber: null, out var number, out var message, out var error);

            Assert.True(ok, error);
            Assert.Equal("4253506007", number);
            Assert.Equal("yo", message);
        }

        [Fact]
        public void TryResolve_FallsBackToDefault_WhenToOmitted()
        {
            var ok = SendSmsTool.TryResolve(
                Args(message: "yo"),
                defaultNumber: "(425) 350-6007", out var number, out _, out var error);

            Assert.True(ok, error);
            Assert.Equal("4253506007", number);
        }

        [Fact]
        public void TryResolve_CallNumberOverridesDefault()
        {
            SendSmsTool.TryResolve(
                Args(message: "yo", to: "555-000-0000"),
                defaultNumber: "425-350-6007", out var number, out _, out _);

            Assert.Equal("5550000000", number);
        }

        [Fact]
        public void TryResolve_MissingMessage_Fails()
        {
            var ok = SendSmsTool.TryResolve(
                Args(message: null, to: "5551234567"),
                null, out _, out _, out var error);

            Assert.False(ok);
            Assert.Contains("message", error);
        }

        [Fact]
        public void TryResolve_NoNumberAnywhere_Fails()
        {
            var ok = SendSmsTool.TryResolve(Args(message: "yo"),
                defaultNumber: null, out _, out _, out var error);

            Assert.False(ok);
            Assert.Contains("number", error);
        }

        [Fact]
        public void Run_Success_InvokesSendWithNumberAndMessage()
        {
            string sentNumber = null, sentMessage = null;
            var result = SendSmsTool.Run(
                Args(message: "hello", to: "425-350-6007"),
                null,
                (num, msg) => { sentNumber = num; sentMessage = msg; });

            Assert.False(result.IsError);
            Assert.Equal("4253506007", sentNumber);
            Assert.Equal("hello", sentMessage);
            Assert.Contains("4253506007", result.Text);
        }

        [Fact]
        public void Run_ValidationError_DoesNotSend()
        {
            var sent = false;
            var result = SendSmsTool.Run(Args(message: "hi"),  // no number
                null, (n, m) => sent = true);

            Assert.True(result.IsError);
            Assert.False(sent);
        }

        [Fact]
        public void Run_SendThrows_ReturnsError()
        {
            var result = SendSmsTool.Run(
                Args(message: "hi", to: "5551234567"),
                null,
                (n, m) => throw new System.Exception("phone link not running"));

            Assert.True(result.IsError);
            Assert.Contains("Failed to send text", result.Text);
            Assert.Contains("phone link not running", result.Text);
        }

        [Fact]
        public void Create_DefinitionExposesSchemaAndReadsDefaultLazily()
        {
            var number = "555-111-1111";
            string sentNumber = null;
            var def = SendSmsTool.Create(() => number, (num, msg) => sentNumber = num);

            Assert.Equal("sendsms", def.Name);
            Assert.Contains("message", def.InputSchema["required"].Values<string>());

            def.Invoke(Args(message: "one"));
            Assert.Equal("5551111111", sentNumber);

            // Change the default; the definition must pick it up without rebuilding.
            number = "555-222-2222";
            def.Invoke(Args(message: "two"));
            Assert.Equal("5552222222", sentNumber);
        }
    }
}
