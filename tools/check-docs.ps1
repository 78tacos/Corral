# Dev helper: sanity-checks the Markdown docs.
#   - no stray control characters (e.g. a mangled "\b" or "\t" in a Windows path)
#   - every relative link points at a file that exists
#   - every #anchor matches a heading in the target file (GitHub-style slugs)
$root = Split-Path $PSScriptRoot -Parent
$files = @('README.md', 'CHANGELOG.md') + (Get-ChildItem (Join-Path $root 'docs') -Filter *.md | ForEach-Object { "docs\$($_.Name)" })
$problems = 0

function Slug([string]$heading) {
    $s = $heading.ToLowerInvariant() -replace '[`*_]', ''
    $s = $s -replace '[^\p{L}\p{N}\s\-]', ''
    ($s.Trim() -replace '\s', '-')
}
function Anchors([string]$path) {
    Get-Content $path -Encoding UTF8 | Where-Object { $_ -match '^#{1,6}\s+(.*)$' } | ForEach-Object { Slug $Matches[1] }
}

foreach ($rel in $files) {
    $path = Join-Path $root $rel
    $text = Get-Content $path -Raw -Encoding UTF8

    foreach ($m in [regex]::Matches($text, '[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]')) {
        "{0}: control character 0x{1:X2} at offset {2}" -f $rel, [int][char]$m.Value, $m.Index; $problems++
    }
    if ($text -match "`t") { "{0}: contains TAB characters" -f $rel; $problems++ }

    # strip fenced code blocks so example text is not parsed as links
    $prose = [regex]::Replace($text, '(?s)```.*?```', '')
    foreach ($m in [regex]::Matches($prose, '\]\(([^)\s]+)\)')) {
        $target = $m.Groups[1].Value
        if ($target -match '^(https?:|mailto:)') { continue }
        $file, $anchor = $target -split '#', 2
        $targetPath = if ($file) { Join-Path (Split-Path $path -Parent) $file } else { $path }
        if (-not (Test-Path $targetPath)) { "{0}: broken link -> {1}" -f $rel, $target; $problems++; continue }
        if ($anchor -and ($targetPath -like '*.md') -and ((Anchors $targetPath) -notcontains $anchor)) {
            "{0}: missing anchor -> {1}" -f $rel, $target; $problems++
        }
    }
}
if ($problems -eq 0) { "docs OK ($($files.Count) files checked)" } else { "$problems problem(s)"; exit 1 }
