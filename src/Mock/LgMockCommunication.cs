using System;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core.Queues;

namespace PepperDash.Essentials.Plugins.Lg.Display.Mock
{
    /// <summary>
    /// In-process stand-in for the display's serial port. Commands sent by the controller are answered
    /// by an <see cref="LgMockResponder"/> on a separate thread, like replies from a real port.
    /// </summary>
    /// <remarks>
    /// Deliberately not an ISocketStatus: the controller treats a comm without socket status as RS232,
    /// which is what makes it start polling and send power-on without "overrideWol".
    /// </remarks>
    public class LgMockCommunication : IBasicCommunication
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private readonly LgMockResponder responder;
        private readonly GenericQueue replyQueue;
        private readonly int responseDelayMs;
        private readonly object bufferLock = new object();
        private string rxBuffer = string.Empty;

        /// <summary>
        /// Creates a mock comm whose replies come from the given responder
        /// </summary>
        public LgMockCommunication(string key, LgMockResponder responder, int responseDelayMs)
        {
            Key = key;
            this.responder = responder;
            this.responseDelayMs = Math.Max(0, responseDelayMs);
            replyQueue = new GenericQueue(key + "-replies");
        }

        /// <inheritdoc />
        public string Key { get; private set; }

        /// <inheritdoc />
        public event EventHandler<GenericCommMethodReceiveBytesArgs> BytesReceived;

        /// <inheritdoc />
        public event EventHandler<GenericCommMethodReceiveTextArgs> TextReceived;

        /// <inheritdoc />
        public bool IsConnected { get; private set; }

        /// <inheritdoc />
        public void Connect()
        {
            IsConnected = true;
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            IsConnected = false;
        }

        /// <inheritdoc />
        public void SendText(string text)
        {
            if (!IsConnected || string.IsNullOrEmpty(text))
            {
                return;
            }

            lock (bufferLock)
            {
                rxBuffer += text;

                int delimiter;
                while ((delimiter = rxBuffer.IndexOf('\r')) >= 0)
                {
                    var line = rxBuffer.Substring(0, delimiter);
                    rxBuffer = rxBuffer.Substring(delimiter + 1);

                    if (line.Trim().Length > 0)
                    {
                        replyQueue.Enqueue(new ProcessStringMessage(line, Reply));
                    }
                }
            }
        }

        /// <inheritdoc />
        public void SendBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return;
            }

            SendText(Latin1.GetString(bytes, 0, bytes.Length));
        }

        // Runs on the reply queue's thread, one command at a time, in send order
        private void Reply(string line)
        {
            if (responseDelayMs > 0)
            {
                CrestronEnvironment.Sleep(responseDelayMs);
            }

            var reply = responder.Respond(line);
            if (reply == null || !IsConnected)
            {
                return;
            }

            var bytesHandler = BytesReceived;
            if (bytesHandler != null)
            {
                bytesHandler(this, new GenericCommMethodReceiveBytesArgs(Latin1.GetBytes(reply)));
            }

            var textHandler = TextReceived;
            if (textHandler != null)
            {
                textHandler(this, new GenericCommMethodReceiveTextArgs(reply));
            }
        }
    }
}
