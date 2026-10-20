using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Transport
{
    internal static class TransportLogText
    {
        public static TextObject Event(string value)
        {
            if (value?.StartsWith("Recovering:") == true)
                return new TextObject("{=ModifiedArmy_TransportRecovering}" +
                    "Recovering resources");
            switch (value)
            {
                case "Dispatched": return Text("Dispatched", "Dispatched");
                case "AvoidingThreat": return Text("AvoidingThreat", "Avoiding threat");
                case "RouteResumed": return Text("RouteResumed", "Route resumed");
                case "RouteBlocked": return Text("RouteBlocked", "Route blocked");
                case "DestinationInvalid": return Text("DestinationInvalid", "Destination invalid");
                case "DestinationInvalidOrExpired": return Text("DestinationInvalidOrExpired", "Destination invalid or expired");
                case "PartialDelivery": return Text("PartialDelivery", "Partial delivery");
                case "TargetCapacityReached": return Text("TargetCapacityReached", "Target capacity reached");
                case "Completed": return Text("Completed", "Completed");
                case "Delivered": return Text("Delivered", "Delivered");
                case "DeadlineExpired": return Text("DeadlineExpired", "Deadline expired");
                case "Recovering": return Text("Recovering", "Recovering resources");
                case "RecoveryPartial": return Text("RecoveryPartial", "Partial recovery");
                case "Recovered": return Text("Recovered", "Recovered");
                default: return new TextObject(value ?? "-");
            }
        }

        private static TextObject Text(string id, string fallback) =>
            new TextObject("{=ModifiedArmy_Transport" + id + "}" + fallback);
    }
}
