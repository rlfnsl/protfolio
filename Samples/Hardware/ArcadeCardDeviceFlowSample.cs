using System;
using System.Collections.Generic;

namespace PortfolioSamples.Hardware
{
    public enum ArcadeDeviceState
    {
        Idle,
        WaitingForPayment,
        SendingDispenseCommand,
        WaitingForCardResponse,
        Completed,
        Failed
    }

    public sealed class ArcadeCardDeviceFlowSample
    {
        private readonly Dictionary<string, string> _responseMessages = new()
        {
            ["00"] = "Success",
            ["F2"] = "No card",
            ["F3"] = "More than one card detected"
        };

        public ArcadeDeviceState State { get; private set; } = ArcadeDeviceState.Idle;
        public string LastResponseCode { get; private set; }

        public void BeginPurchase(int requiredCoin, int currentCoin)
        {
            State = currentCoin >= requiredCoin
                ? ArcadeDeviceState.SendingDispenseCommand
                : ArcadeDeviceState.WaitingForPayment;
        }

        public byte[] BuildDispenseCommand(int slotIndex)
        {
            State = ArcadeDeviceState.WaitingForCardResponse;
            return new byte[] { 0x02, (byte)slotIndex, 0x03 };
        }

        public bool ApplyDeviceResponse(string responseCode, out string message)
        {
            LastResponseCode = responseCode;

            if (_responseMessages.TryGetValue(responseCode, out message) && responseCode == "00")
            {
                State = ArcadeDeviceState.Completed;
                return true;
            }

            message ??= "Unknown device response";
            State = ArcadeDeviceState.Failed;
            return false;
        }

        public void Reset()
        {
            LastResponseCode = null;
            State = ArcadeDeviceState.Idle;
        }
    }
}

