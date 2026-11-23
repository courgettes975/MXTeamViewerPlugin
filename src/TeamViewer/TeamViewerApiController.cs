namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;

    // TeamViewer Web API (https://webapi.teamviewer.com/api/v1). It manages accounts, devices and
    // connection reports; it has NO endpoint to control the local client (mic, audio, recording,
    // zoom...). It is therefore only used for an optional token check in the diagnostics.
    // The token is read from the DPAPI store (scripts\Set-ApiToken.ps1), never from config.json,
    // and never logged.
    internal sealed class TeamViewerApiController
    {
        public const string TokenSecretName = "teamviewer-api-token";
        private const string BaseUrl = "https://webapi.teamviewer.com/api/v1/";

        private readonly Func<PluginConfig> _config;

        public TeamViewerApiController(Func<PluginConfig> config)
        {
            this._config = config;
        }

        public bool Enabled => this._config().ApiEnabled;

        public bool HasToken => SecretStore.Has(TokenSecretName);

        // GET /ping -> {"token_valid": true|false}
        public string CheckToken()
        {
            if (!this.Enabled)
            {
                return "désactivée (apiEnabled=false)";
            }
            var token = SecretStore.Get(TokenSecretName);
            if (string.IsNullOrEmpty(token))
            {
                return "aucun jeton (scripts\\Set-ApiToken.ps1)";
            }
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var body = http.GetStringAsync("ping").GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.TryGetProperty("token_valid", out var v) && v.GetBoolean() ? "jeton valide" : "jeton refusé";
            }
            catch (Exception ex)
            {
                return "erreur: " + TvLog.Redact(ex.Message);
            }
        }
    }
}
