# OEM identity and Windows collection verification

Verified on 2026-10-14. The Windows collector retains its original progress/cancellation API and accepts an optional third `IProgress<ScanProgress>` argument. Four completed acquisition stages precede ten timed samples (steps 5–14). Each sample event carries the same `MetricSample` object stored in the resulting snapshot. No synthetic usage or temperature is introduced, and the existing read-only sensor configuration remains intact.

`DeviceClassifier` uses documented `PCSystemType` and SMBIOS chassis evidence. Missing or contradictory firmware evidence yields Unknown. A UPS/battery alone cannot classify a desktop as a laptop. `IsLaptop` now follows the classified type. WMI reads product Name/Version, SystemFamily and SystemSKUNumber, without requesting product IdentifyingNumber, chassis SerialNumber, or BIOS SerialNumber during a normal scan.

## Verified Lenovo sources

- [Official LOQ 15IAX9 type 83GS driver portal](https://pcsupport.lenovo.com/us/en/products/laptops-and-netbooks/gaming-series/loq-15iax9/83gs/downloads/). The official short form `/us/en/products/83GS/downloads/` redirects to this page and embeds `ds_productinfo` containing ProductName and MachineType. The service verifies those fields before accepting a model match.
- [Official model catalog endpoint](https://pcsupport.lenovo.com/us/en/api/v4/downloads/drivers?productId=83GS). Verified live: `message: succeed`, `body.DownloadItems`; packages expose Title, Category.Name, SummaryInfo.Version, OperatingSystemKeys, DocId and Files (URL/TypeString/Date.Unix). Dates are Unix milliseconds. This public website endpoint is not represented as a stable, formally supported SDK contract; response changes result in a truthful portal fallback.
- [Toolkit PC support downloader source](https://github.com/BartoszCichecki/LenovoLegionToolkit/blob/master/LenovoLegionToolkit.Lib/PackageDownloader/PCSupportPackageDownloader.cs) identifies that same catalog endpoint/schema.
- [Toolkit Vantage downloader source](https://github.com/BartoszCichecki/LenovoLegionToolkit/blob/master/LenovoLegionToolkit.Lib/PackageDownloader/VantagePackageDownloader.cs) separately uses `download.lenovo.com/catalog/{machineType}_{os}.xml` and package detection rules. Silen does not implement those installed-device update-detection rules, and therefore labels results as model catalog packages rather than verified updates.
- [Original Toolkit license](https://github.com/BartoszCichecki/LenovoLegionToolkit/blob/master/LICENSE) is GNU GPL version 3. The implementation here is independently written; no Toolkit implementation code, dependencies, drivers, installers, or assets are incorporated. Toolkit served as endpoint and behavior reference.

The default lookup derives the four-character Lenovo machine type from a valid model/MTM or a structured `LENOVO_MT_...` SKU. It verifies the official product page and retrieves the model catalog. It does not guess a model from a marketing string, and it does not compare catalog packages to installed PnP devices. Package metadata shows compatible operating systems instead of assuming that a returned package fits the current OS or individual hardware.

Explicit `useSerial: true` invokes a transient, cancellable WMI BIOS serial read. Empty/generic/placeholder values are rejected before HTTP. Only the explicitly requested Lenovo product lookup receives that serial; catalog retrieval subsequently uses the returned machine type. The service has no serial logging, history, file writes, or serial cache. Results contain a masked serial and a model-only portal URL; server-echoed serial text is masked and URLs containing it are discarded. Requests use no-store/no-cache and an official base referrer. Default/model lookup never calls the serial reader. Live serial lookup was deliberately not performed during verification.

HTTP is asynchronous, cancellable, and bounded to 20 seconds for the complete lookup. Redirects remain restricted to exact HTTPS `pcsupport.lenovo.com` with standard port and no user info; at most four responses are followed. Malformed, not-found, unavailable and timed-out responses retain an honest official portal fallback. Catalog URLs are accepted only from exact HTTPS `download.lenovo.com`; detail URLs are constructed solely from validated Lenovo `DS` document IDs. Links open in the browser, with no installer launch arguments, download automation or install action.

## Other OEM portals

The following verified official portals are offered with the detected model/SKU and an explanation that the app has no verified native catalog for that provider. `ModelMatched` remains false; no per-model path, packages or availability are invented.

| Manufacturer | Official source |
| --- | --- |
| Dell | [Drivers/support portal](https://www.dell.com/support/home/en-us?app=drivers) |
| HP | [Driver portal](https://support.hp.com/drivers), [official model-selection instructions](https://support.hp.com/ca-en/document/ish_4987298-4987376-16) |
| ASUS | [Download Center](https://www.asus.com/support/download-center/) |
| Acer | [Drivers and Manuals](https://www.acer.com/us-en/support/drivers-and-manuals) |
| MSI | [Support](https://www.msi.com/support) |
| Microsoft Surface device | [Surface support](https://support.microsoft.com/surface) |
| Microsoft generic/inbox driver provider | [Microsoft Update Catalog](https://www.catalog.update.microsoft.com/) |

The HP portal returned a fetch restriction in the web verification environment; official HP documentation confirms the portal's model-based driver workflow. Generic Microsoft driver entries previously opened Surface support despite no Surface device identity; they now open the Update Catalog. Surface-specific OEM support is selected only when a Microsoft machine actually identifies as Surface.

## Evidence

- TDD red: classifier returned Unknown and seven assertions failed for documented laptop/desktop evidence and desktop+UPS. Implemented classifier then passed all ten classifier cases.
- TDD red: actual read-only collector produced no detailed events; assertion failed expecting steps 1–14. The implemented collector passed events, identical sample references, ten timestamps, bounded usage and existing profile/hardware-ID privacy checks.
- TDD red: 21 OEM/actions assertions failed against the service skeleton and original action mappings. Tests now cover live-source HTTP fixtures, verified identity mismatch, catalog metadata, model/SKU derivation, default serial isolation, eleven invalid serial variants, explicit masked serial output, cancellation, timeout, offline/not-found/malformed responses, unverified OEM fallbacks and malicious download links. Additional cases cover unapproved redirects, verified empty catalogs and actual Surface identity.
- The fixtures retain two complete official catalog entries (NVIDIA DS565703 and Intel DS565702) from the 83GS endpoint. The product-page fixture retains the official product object with its anonymous session UserId removed. Neither fixture contains a real device serial.
- A single live service verification of the default model-only call returned `Success=true`, `ModelMatched=true`, `DeviceName=LOQ 15IAX9 - Type 83GS`, and **28 packages** across 14 categories. No executable was downloaded or run. Enable the opt-in test with `SILEN_LIVE_OEM_CHECK=1` and filter `LiveOemVerificationTests`; ordinary suite runs skip that network test.
- Final Windows project suite: **109 passed, 0 failed, 1 explicitly skipped live-network test**, including the real read-only scan. The separately enabled live test passed **1/1**. `git diff --check` reported no whitespace errors in the changed tracked Windows/test files.

Classification source: [Win32_ComputerSystem](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystem), [Win32_SystemEnclosure](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-systemenclosure), and [Win32_ComputerSystemProduct](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystemproduct).

## Review follow-up: persisted null identity and contradictory serial

The independent review reproduced two defects. JSON deserialization can populate nominally non-null MachineInfo strings with null; direct Trim/StartsWith calls then threw before the network error handling. Eight new regressions first failed with NullReferenceException. They exercise persisted all-null/partially-null records, a null Lenovo model with usable SKU, missing Lenovo model/SKU, a Microsoft non-Surface identity with null model, an unknown null manufacturer, and the direct model/SKU derivation boundary. The service now normalizes nullable manufacturer/model values and treats null SKU as absent, preserving the existing honest official-portal fallback and valid SKU lookup.

An explicit serial request also previously accepted official metadata that contained a different non-empty Serial and continued to the catalog. The new contradictory-serial regression first failed because the catalog was requested. The metadata parser now carries that serial only transiently for comparison, and the service rejects a mismatch before assigning ModelMatched or requesting packages. Requested and returned raw serials do not appear in result/error data. Matching serials are accepted case-insensitively; absent and empty official Serial fields retain the previous valid behavior. Three additional regressions cover those accepted cases.

Focused verification after both fixes: **93 passed, 0 failed, 0 skipped** using filters for OemDriverServiceTests, DeviceClassifierTests and WindowsActionsTests. This includes 49 OEM service cases and twelve new regression cases. No live-network lookup, whole-solution test, publish, installer download or installation was performed during this follow-up.
