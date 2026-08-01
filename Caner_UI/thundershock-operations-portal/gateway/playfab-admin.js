export class PlayFabAdminClient {
  constructor(titleId, secretKey, whitelistReaderId, whitelistKey) {
    this.titleId = titleId;
    this.secretKey = secretKey;
    this.whitelistReaderId = whitelistReaderId;
    this.whitelistKey = whitelistKey;
    this.baseUrl = `https://${titleId}.playfabapi.com`;
  }

  async call(path, body, headers = {}) {
    let lastError;
    for (let attempt = 0; attempt < 3; attempt++) {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 15_000);
      try {
        const response = await fetch(this.baseUrl + path, {
          method: "POST",
          headers: { "Content-Type": "application/json", ...headers },
          body: JSON.stringify(body),
          signal: controller.signal,
        });
        const result = await response.json().catch(() => ({}));
        if (response.ok && result.code === 200) return result.data;
        const error = new Error(result.errorMessage || result.error || `PlayFab HTTP ${response.status}`);
        if (response.status !== 429 && response.status < 500) throw Object.assign(error, { retryable: false });
        lastError = error;
      } catch (error) {
        lastError = error;
        if (error.retryable === false) throw error;
      } finally {
        clearTimeout(timeout);
      }
      await new Promise((resolve) => setTimeout(resolve, 250 * 2 ** attempt));
    }
    throw lastError || new Error("PlayFab request failed");
  }

  async roster() {
    const login = await this.call("/Client/LoginWithCustomID", {
      TitleId: this.titleId,
      CustomId: this.whitelistReaderId,
      CreateAccount: false,
    });
    const titleData = await this.call(
      "/Client/GetTitleData",
      { Keys: [this.whitelistKey] },
      { "X-Authorization": login.SessionTicket },
    );
    const raw = titleData.Data?.[this.whitelistKey];
    if (!raw) throw new Error(`PlayFab Title Data '${this.whitelistKey}' is missing`);
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed.players) ? parsed.players : [];
  }

  async playFabId(employeeId) {
    const login = await this.call("/Client/LoginWithCustomID", {
      TitleId: this.titleId,
      CustomId: employeeId,
      CreateAccount: false,
    });
    return login.PlayFabId;
  }

  async privacy(type, employeeId) {
    if (!this.secretKey) throw new Error("PLAYFAB_SECRET_KEY is not configured");
    const playFabId = await this.playFabId(employeeId);
    const endpoint = type === "export"
      ? "/Admin/ExportMasterPlayerData"
      : type === "delete"
        ? "/Admin/DeleteMasterPlayerAccount"
        : null;
    if (!endpoint) throw new Error("Unsupported privacy request");
    const result = await this.call(endpoint, {
      PlayFabId: playFabId,
      ...(type === "delete" ? { MetaData: `CEDAS:${employeeId}` } : {}),
    }, { "X-SecretKey": this.secretKey });
    return {
      jobReceiptId: result.JobReceiptId,
      titleIds: result.TitleIds || [this.titleId],
      status: "accepted",
    };
  }
}
