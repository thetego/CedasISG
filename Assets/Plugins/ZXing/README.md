# ZXing.Net

- Version: 0.16.9 (netstandard2.0 build)
- Source: https://github.com/micjahn/ZXing.Net
- License: Apache-2.0 (https://licenses.nuget.org/Apache-2.0)

Used by `QRScannerManager` (Assets/_Project/Scripts/QR/) to decode QR codes from
the device camera feed (`WebCamTexture.GetPixels32()`).

Unity will generate a `.meta` file for `zxing.dll` the next time the project is
opened in the Editor. After that, select `zxing.dll` in the Project window and
confirm under Inspector -> Select platforms for plugin that Editor, Android and
iOS are all enabled (they should be checked by default).
