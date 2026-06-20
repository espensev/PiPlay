#Requires -Version 5.1
<#
.SYNOPSIS
  Local preflight that predicts the CI "Require design spec" (spec-check) gate before you open a PR.

.DESCRIPTION
  Replays the soft gate in .github/workflows/spec-check.yml against THIS branch's changed files so a
  red gate is caught locally instead of after pushing. It unions the committed diff vs the base branch
  (git diff main...HEAD) with staged, unstaged, and untracked changes, because pre-commit work (a new
  dated spec, or the gated src/tests edits themselves) lives in the working tree, not yet in any PR.

  SOURCE OF TRUTH: .github/workflows/spec-check.yml. The regexes below are copied VERBATIM from that
  workflow (lines 56 / 63 / 71); keep them in sync. The PowerShell forms are EXACT translations of the
  grep -E forms, with two non-obvious fidelity points:
    * grep -E is case-SENSITIVE, but PowerShell -match is case-INSENSITIVE by default. The gated and
      dated-spec checks therefore use -cmatch (case-sensitive). A default -match would FALSELY PASS a
      file like "...-DESIGN.MD" locally that CI then rejects.
    * the Spec-Exception line is matched case-insensitively (grep -qiE) and per-line in a multi-line
      body. Using \s* would let \s swallow the CRLF and falsely match a whitespace-only reason; the
      inner whitespace is narrowed to [^\S\r\n] so it cannot cross a line.

    gated paths   (yml: grep -E '^(src|scripts|tests)/')
      ps: -cmatch '^(src|scripts|tests)/'
    dated spec    (yml: grep -E '^docs/superpowers/specs/[0-9]{4}-[0-9]{2}-[0-9]{2}-.+-design\.md$')
      ps: -cmatch '^docs/superpowers/specs/[0-9]{4}-[0-9]{2}-[0-9]{2}-.+-design\.md$'
    exception     (yml: grep -qiE '^[[:space:]]*Spec-Exception:[[:space:]]*[^[:space:]].*$')
      ps: -match  '(?mi)^[^\S\r\n]*Spec-Exception:[^\S\r\n]*\S.*$'

  This mirrors the gate's decision order and fail-closed intent; it does not call GitHub, so the PR
  body is supplied locally via -PrBody / -PrBodyFile to model the Spec-Exception escape hatch.

.EXAMPLE
  pwsh -File scripts\Preflight-SpecGate.ps1
.EXAMPLE
  pwsh -File scripts\Preflight-SpecGate.ps1 -PrBody "Spec-Exception: urgent hotfix, spec to follow"
.EXAMPLE
  pwsh -File scripts\Preflight-SpecGate.ps1 -PrBodyFile .git\PR_BODY.txt
#>
[CmdletBinding()]
param(
    [string]$BaseBranch = "main",
    [string]$PrBody,
    [string]$PrBodyFile,
    [switch]$Quiet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "NativeCommand.ps1")

$repoRoot = Split-Path -Parent $PSScriptRoot

# --- regexes (verbatim translations of spec-check.yml; see header SOURCE OF TRUTH) ---
$GatedRe     = '^(src|scripts|tests)/'
$SpecRe      = '^docs/superpowers/specs/[0-9]{4}-[0-9]{2}-[0-9]{2}-.+-design\.md$'
$ExceptionRe = '(?mi)^[^\S\r\n]*Spec-Exception:[^\S\r\n]*\S.*$'

function Write-Ok([string]$m)   { if (-not $Quiet) { Write-Host "[ OK ] $m" -ForegroundColor Green } }
function Write-Note([string]$m) { if (-not $Quiet) { Write-Host "[NOTE] $m" -ForegroundColor Cyan } }
function Write-Warn2([string]$m){ Write-Host "[WARN] $m" -ForegroundColor Yellow }
function Write-Fail([string]$m) { Write-Host "[FAIL] $m" -ForegroundColor Red }

function Invoke-Git([string[]]$GitArgs) {
    # core.quotepath=false makes git emit raw UTF-8 paths instead of octal-escaped, double-quoted
    # non-ASCII names (e.g. "src/caf\303\251.cs"). The leading quote would otherwise defeat the
    # anchored '^(src|scripts|tests)/' match — CI sees clean REST .filename values, so we match those.
    $out = Invoke-NativeCommandQuiet { & git -C $repoRoot -c core.quotepath=false @GitArgs }
    return ,@($out)
}

# --- gather changed files: committed (base...HEAD) + staged + unstaged + untracked ---
$set = [System.Collections.Generic.HashSet[string]]::new()
function Add-Files([string[]]$files) {
    foreach ($f in $files) {
        if ([string]::IsNullOrWhiteSpace($f)) { continue }
        [void]$set.Add(($f -replace '\\', '/').Trim())
    }
}

# Resolve the base. CI fails CLOSED when it cannot determine the PR's changed files (spec-check.yml:
# an empty/undeterminable list -> '::error:: ... failing closed' exit 1). If the base branch is
# missing we do the same, rather than continue on a working-tree-only view: a gated change that is
# already committed lives only in base...HEAD, so dropping the base diff would silently miss it
# (a fail-OPEN divergence from CI). Stays offline / no-origin per the design non-goal — resolve the
# base locally (e.g. `git branch main <ref>`) instead of fetching.
$null = Invoke-Git @("rev-parse", "--verify", "--quiet", "$BaseBranch^{commit}")
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Base branch '$BaseBranch' not found locally; cannot model the committed diff the PR will show."
    Write-Fail "CI fails closed when it can't determine changed files, so this preflight does too. Resolve the base (e.g. 'git branch $BaseBranch <ref>', or pass -BaseBranch) and re-run."
    exit 1
}
Add-Files (Invoke-Git @("diff", "--name-only", "$BaseBranch...HEAD"))
Add-Files (Invoke-Git @("diff", "--name-only", "--cached"))
Add-Files (Invoke-Git @("diff", "--name-only"))
Add-Files (Invoke-Git @("ls-files", "--others", "--exclude-standard"))

# Wrap @() AROUND the whole pipeline so $changed is always [array] — at 0 files Sort-Object yields
# $null and at 1 file a bare [String], and under Set-StrictMode both .Count accesses throw.
$changed = @($set | Sort-Object)

if ($changed.Count -eq 0) {
    Write-Ok "No changed files detected vs '$BaseBranch' (committed/staged/unstaged/untracked). Spec gate would not run."
    exit 0
}

Write-Note "Changed files considered ($($changed.Count)):"
if (-not $Quiet) { $changed | ForEach-Object { Write-Host "    $_" } }

# --- PR body for the Spec-Exception escape hatch (optional, supplied locally) ---
$body = ""
if ($PrBodyFile) {
    if (-not (Test-Path -LiteralPath $PrBodyFile)) { throw "PrBodyFile not found: $PrBodyFile" }
    $body = Get-Content -LiteralPath $PrBodyFile -Raw
} elseif ($PrBody) {
    $body = $PrBody
}

# --- replay the gate, in the same order as spec-check.yml ---
$gated = @($changed | Where-Object { $_ -cmatch $GatedRe })
if ($gated.Count -eq 0) {
    Write-Ok "No changes under src/, scripts/, or tests/ - design-spec gate not required. PASS"
    exit 0
}

Write-Note "Gated code paths touched:"
if (-not $Quiet) { $gated | ForEach-Object { Write-Host "    $_" } }

$spec = @($changed | Where-Object { $_ -cmatch $SpecRe })
if ($spec.Count -gt 0) {
    Write-Ok "Dated design spec present:"
    if (-not $Quiet) { $spec | ForEach-Object { Write-Host "    $_" } }
    Write-Ok "Spec gate would PASS."
    exit 0
}

if ($body -and ($body -match $ExceptionRe)) {
    $reason = ([regex]::Match($body, $ExceptionRe)).Value.Trim()
    Write-Warn2 "Spec gate would PASS via exception - $reason"
    Write-Warn2 "Put this exact line in the PR description so CI records it."
    exit 0
}

Write-Fail "This branch changes code under src/, scripts/, or tests/ but adds no dated design spec matching docs/superpowers/specs/YYYY-MM-DD-*-design.md (see docs/Feature_Workflow.md)."
Write-Fail "Add one, or add a line 'Spec-Exception: <reason>' to the PR description to override deliberately."
Write-Fail "Spec gate would FAIL."
exit 1
