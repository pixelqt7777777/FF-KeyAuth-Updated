KEYAUTH 1.3 UPDATE

Changed:
- KeyAuth.cs replaced with a compact KeyAuth API 1.3 client.
- KeyAuthConfig.cs updated to:
    App Name: PIRATES CORPORATION
    Owner ID: v9EmMZbZqa
    Version: 1.0
- KeyAuthAppService.cs updated for the 3-argument API constructor.
- Existing license-key and username/password login UI kept intact.
- Old bin/ and obj/ folders were intentionally removed so the project builds fresh.

Important:
- No Seller API key is embedded.
- This ZIP was not compiled in ChatGPT's environment because the .NET SDK is not installed there.
- Microsoft Defender previously flagged the old compiled TANISH REGEDIT.dll, so do not restore the old bin/obj outputs. Build fresh and scan the new output.
