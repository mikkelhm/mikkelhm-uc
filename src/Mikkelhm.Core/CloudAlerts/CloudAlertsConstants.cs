namespace Mikkelhm.Core.CloudAlerts;

public static class CloudAlertsConstants
{
    public const string HomeAlias = "cloudAlertsHome";
    public const string AlertAlias = "cloudAlert";

    public static class Properties
    {
        public const string AlertId = "alertId";
        public const string ProjectAlias = "projectAlias";
        public const string ProjectUrl = "projectUrl";
        public const string EnvironmentName = "environmentName";
        public const string AlertName = "alertName";
        public const string Details = "details";
        public const string Severity = "severity";
        public const string CustomerMetadata = "customerMetadata";
        public const string TimeFired = "timeFired";
        public const string IsTest = "isTest";
        public const string RawPayload = "rawPayload";
    }
}
