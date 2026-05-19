# PDF Flyer Downloader

Small .NET console app that downloads a flyer PDF from a direct URL and saves it locally as:

`<source>_<yyyy-MM-dd>.pdf`

Example: `toom_2026-04-20.pdf`

## Approach

I organized the solution with **separation of concerns** and SOLID-oriented design:

- `Program.cs`: composition root, wiring dependencies and process exit codes.
- `Core/`: business workflow + abstractions (`FlyerDownloader`, interfaces, validators, domain exception).
- `Infrastructure/`: external concerns (HTTP client, Polly retry pipeline, filesystem persistence, Serilog setup).
- `Models/`: request/options model and parsing.

This keeps orchestration, validation, I/O, and resiliency in focused units instead of one large file.

## Tools / Libraries

- **.NET 8 Console App**
- **HttpClient** for downloading
- **Polly** for retries (exponential backoff + jitter)
- **Serilog + Console sink** for structured logs

## Run

```bash
dotnet restore
dotnet run --project PdfDownloader -- --source=toom --url=https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf --output=./downloads
```

Arguments:

- `--source=<name>`: flyer source name used in filename (default: `toom`)
- `--url=<pdf-url>`: direct PDF URL (default: W3C dummy PDF)
- `--output=<path>`: output directory (default: `<app>/downloads`)

## Error handling included

The app handles and logs:

- network / timeout errors
- non-success HTTP response
- invalid/missing content type
- empty payload
- invalid PDF header
- invalid CLI input (empty source, malformed URL)
- unexpected exceptions (fatal log)

Exit codes:

- `0` success
- `1` unexpected runtime failure
- `2` expected download/validation failure

## Known limitations

- Assumes a **direct** PDF URL (no dynamic page scraping).
- Only basic PDF validation is done (header check), not full PDF parsing.
- No checksum/signature verification from source system.
- Uses UTC date for naming; local timezone naming might be desired depending on business rules.

## What I would improve with more time

- Add unit tests per class (`FlyerDownloader`, validators, options parser) and integration tests with mocked HTTP.
- Add config file + environment variable support.
- Add HEAD pre-check and content-length safety limits.
- Add metrics/telemetry export (OpenTelemetry).
- Add richer logging sinks (rolling file, centralized logging).
- Add batch mode for multiple sources.
