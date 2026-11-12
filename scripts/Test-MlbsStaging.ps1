param(
    [string]$ApiBaseUrl = 'https://mmsstaging.modena-app.co.za/mlbsapi/',
    [string]$ClientClassicAddress = 'rBsNGrUnSzaCgBi3cnjMvACTzyiEpiXc5r',
    [string]$ProductCode = '50554C5345',
    [switch]$TestSignIn,
    [string]$Email,
    [string]$ReportPath
)
# PowerShell 7. Default checks are read-only. Sign-in can affect subscription tokens.
$ErrorActionPreference = 'Stop'
if ($TestSignIn -and [string]::IsNullOrWhiteSpace($Email)) { throw '-Email is required with -TestSignIn.' }
$base = $ApiBaseUrl.TrimEnd('/') + '/'
$results = [Collections.Generic.List[object]]::new()
function Invoke-Probe([string]$Name, [string]$Route, [hashtable]$Body, [int]$ExpectedHttp) {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $response = Invoke-WebRequest -Uri ($base + 'api/User/' + $Route) -Method Post -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Compress) -TimeoutSec 30 -SkipHttpErrorCheck
        $data = $null
        try { $data = $response.Content | ConvertFrom-Json } catch { }
        $result = [ordered]@{
            name = $Name; httpStatus = [int]$response.StatusCode; expectedHttp = $ExpectedHttp
            passed = [int]$response.StatusCode -eq $ExpectedHttp
            durationMs = $watch.ElapsedMilliseconds
        }
        if ($Route -eq 'get-product-code' -and $data.tokenCodes) {
            $result.success = $data.tokenCodes.success
            $result.status = $data.tokenCodes.status
            $result.licenseCode = $data.tokenCodes.licenseCode
            $result.subscriptionCode = $data.tokenCodes.subscriptionCode
            $result.passed = $result.passed -and $data.tokenCodes.success
        } elseif ($Route -like '*balance' -and $response.StatusCode -eq 200) {
            $result.balance = $data.tokenBalance
            $result.limit = $data.tokenLimit
            $result.currency = $data.currencyCode
            $result.passed = $result.passed -and $null -ne $data.tokenBalance
        } else {
            $result.status = $data.status
            $result.message = if ($data.signInMessage) { $data.signInMessage } elseif ($data.registerUserMessage) { $data.registerUserMessage } elseif ($data.message) { $data.message } elseif ($response.StatusCode -eq 400 -and -not $data) { $response.Content.Substring(0, [Math]::Min(200, $response.Content.Length)) } else { $null }
            if ($Route -eq 'sign-in') { $result.signedIn = $data.signInSuccess }
        }
        $results.Add([pscustomobject]$result)
    } catch {
        $results.Add([pscustomobject]@{name=$Name; passed=$false; error=$_.Exception.Message})
    }
}
Invoke-Probe 'PULSE product and tokens configured' 'get-product-code' @{TokenCode=$ProductCode} 200
Invoke-Probe 'Licence balance before' 'get-license-balance' @{ClassicAddress=$ClientClassicAddress;TokenCode=$ProductCode} 200
Invoke-Probe 'Subscription balance before' 'get-subscription-balance' @{ClassicAddress=$ClientClassicAddress;TokenCode=$ProductCode} 200
Invoke-Probe 'Reject malformed product code' 'get-product-code' @{TokenCode='NOT-HEX'} 400
Invoke-Probe 'Reject missing wallet address' 'get-license-balance' @{ClassicAddress='';TokenCode=$ProductCode} 400
Invoke-Probe 'Reject incomplete sign-in' 'sign-in' @{TokenCode=$ProductCode} 400
Invoke-Probe 'Reject invalid email' 'sign-in' @{ClientClassicAddress=$ClientClassicAddress;UserEmail='not-an-email';TokenCode=$ProductCode} 400
Invoke-Probe 'Reject incomplete registration' 'register-user' @{TokenCode=$ProductCode} 400
Invoke-Probe 'Reject invalid registration email' 'register-user' @{ClientClassicAddress=$ClientClassicAddress;UserEmail='not-an-email';TokenCode=$ProductCode} 400
if ($TestSignIn) {
    # An unregistered product user should receive a useful 404/403; never register
    # automatically, retry a charge, or consume tokens through a separate endpoint.
    Invoke-Probe 'Existing test identity sign-in' 'sign-in' @{ClientClassicAddress=$ClientClassicAddress;UserEmail=$Email;TokenCode=$ProductCode} 200
    Invoke-Probe 'Licence balance after' 'get-license-balance' @{ClassicAddress=$ClientClassicAddress;TokenCode=$ProductCode} 200
    Invoke-Probe 'Subscription balance after' 'get-subscription-balance' @{ClassicAddress=$ClientClassicAddress;TokenCode=$ProductCode} 200
}
$report = [ordered]@{ checkedAtUtc=[DateTime]::UtcNow.ToString('o'); apiBaseUrl=$base; productCode=$ProductCode; clientClassicAddress=$ClientClassicAddress; results=@($results.ToArray()) }
$json = $report | ConvertTo-Json -Depth 6
if ($ReportPath) { $json | Set-Content -LiteralPath $ReportPath -Encoding UTF8 }
$json
