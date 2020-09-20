[CmdletBinding()]
param ()

$oldPwd = $PWD
$scriptHome = Split-Path $MyInvocation.MyCommand.Path
Set-Location $scriptHome

###
# Assemblies
###
Add-Type -AssemblyName "System.Threading.Tasks"
Add-Type -Path ".\BatchDownloader.dll"

###
# Const
###
Set-Location $oldPwd
$proxiesFile = "$scriptHome\batch-loader-proxies.bin"
$userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:79.0) Gecko/20100101 Firefox/79.0"

$loader = New-Object -TypeName "BatchDownloader.Loader" -ArgumentList @($userAgent)

$global:savePath = $oldPwd
$global:savePrefix = (Get-Date -UFormat "%Y%m%d_%H.%M.%S")
$global:downloadIndex = 0

function isURIWeb($address) {
	$uri = $address -as [System.URI]
	$null -ne $uri.AbsoluteURI -and $uri.Scheme -match '[http|https]'
}

#https://stackoverflow.com/questions/51218257/await-async-c-sharp-method-from-powershell
function Await-Task {
    param (
        [Parameter(ValueFromPipeline=$true, Mandatory=$true)]
        $task
    )

    process {
        while (-not $task.AsyncWaitHandle.WaitOne(200)) { }
        $task.GetAwaiter().GetResult()
    }
}

function AddProxy {
    [CmdletBinding()]
    param (
        # Proxy address (eg. 1.2.3.4:5678)
        [Parameter(Mandatory)]
        [string]
        $ProxyAddress,
        # Proxy auth required
        [Parameter(Mandatory=$false)]
        [switch]
        $ProxyAuth,
        # Proxy username
        [Parameter(Mandatory=$false)]
        [string]
        $ProxyUser,
        # Proxy password
        [Parameter(Mandatory=$false)]
        [string]
        $ProxyPass
    )
    process {
        $p = $null
        try {
            if ($ProxyAuth) {
                if ($ProxyUser -ne "" -and $ProxyPass -ne "") {
                    $p = $loader.AddProxy($ProxyAddress, $ProxyUser, $ProxyPass)
                } else {
                    $creds = Get-Credential -Message "Enter proxy credentials"
                    $p = $loader.AddProxy($ProxyAddress, $creds.UserName, $creds.Password)
                }
            } else {
                $p = $loader.AddProxy($ProxyAddress)
            }
            Write-Verbose -Message ("Created proxy: {1}{0}" -f $p.Address, ($ProxyAuth ? $p.Credentials.UserName + "@" : ""))
        }
        catch {
            Write-Error "Failed to add proxy: $_"
            Write-Debug $_.ScriptStackTrace
            return $false
        }
    }
}

function SaveProxies {
    [CmdletBinding()]
    param (
        # File path, that stores proxies info
        [Parameter(Mandatory=$false)]
        [string]
        $SavePath = $proxiesFile
    )
    
    process {
        if ($proxies.Count -eq 0) {
            return $true
        }
        try {
            $bf = New-Object System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
            $fs = [System.IO.File]::Create($SavePath)
            $htable = ($loader.Proxies | ForEach-Object -Process {
                @{
                    Address = $_.Address.OriginalString;
                    Auth = $null -ne $_.Credentials;
                    UserName = $_.Credentials.UserName; 
                    Password = $_.Credentials.Password;
                }
            })
            $bf.Serialize($fs, $htable)
            $fs.Close()
        }
        catch {
            Write-Error "Failed to create save file: $_"
            Write-Debug $_.ScriptStackTrace
            return $false
        }

        Write-Verbose ("Saved proxies ({0}) to file: {1}" -f $loader.Proxies.Count, $SavePath)
        return $true
    }
}

function LoadProxies {
    param (
        # File path, that stores proxies info
        [Parameter(Mandatory=$false)]
        [string]
        $SavePath = $proxiesFile
    )
    
    process {
        if (-not (Test-Path $SavePath)) {
            Write-Debug "Save file not found"
            return $false
        }

        try {
            $bf = New-Object System.Runtime.Serialization.Formatters.Binary.BinaryFormatter
            $fs = [System.IO.File]::OpenRead($SavePath)
            $htable = $bf.Deserialize($fs)
            $fs.Close()

            $htable | ForEach-Object {
                AddProxy -ProxyAddress $_.Address -ProxyAuth:$_.Auth -ProxyUser $_.UserName -ProxyPass $_.Password
            }
        }
        catch {
            Write-Error "Failed to read save file: $_"
            Write-Debug $_.ScriptStackTrace
            return $false
        }

        Write-Debug ("Loaded proxies ({0}) from file: {1}" -f $loader.Proxies.Count, $SavePath)
        return $true
    }
}

function PrintProxies {
    param ()
    process {
        $loader.Proxies | ForEach-Object {$index=0} {$_; $index++} | Format-Table @{
            Label="Index"; Expression={$index}
        }, @{
            Label="Address"; Expression={$_.Address}
        }, @{
            Label="UserName"; Expression={$_.Credentials ? $_.Credentials.UserName : ""}
        }
    }    
}

function RemoveProxy {
    param (
        # Index of proxy to remove
        [Parameter(Mandatory)]
        [uint]
        $Index
    )
    process {
        if ($Index -ge $proxies.Count) {
            return
        }
        Write-Host ("Removed proxy #{0} ({1})" -f $Index, $proxies[$Index].Address)
        $loader.RemoveProxy($Index)
    }
}

function DownloadFile {
    param (
        # Url
        [Parameter(Mandatory)]
        [string]
        $UrlString
    )
    process {
        $u = [uri]$UrlString
        if (-not (isURIWeb($u))) {
            Write-Host "Invalid url: $UrlString"
            return
        }

        $file = [System.IO.Path]::Combine(
            [System.IO.Path]::GetFullPath($savePath), 
            ("{0}_{1}_{2}" -f $savePrefix, $downloadIndex, $u.Segments[$u.Segments.Count - 1])
        )

        $q = $loader.DownloadFile($u, $file) | Await-Task
        Write-Verbose $q

        $global:downloadIndex++
    }
}

function SetPath {
    param (
        # Path to save to
        [Parameter()]
        [string]
        $SavePath
    )
    process {
        try {
            $SavePath = [System.IO.Path]::GetFullPath($SavePath)
            if (-not ([System.IO.Directory]::Exists($SavePath))) {
                [void]( [System.IO.Directory]::CreateDirectory($SavePath) )
            }
            $global:savePath = $SavePath
            Write-Host "Path set to: $SavePath"
        }
        catch {
            Write-Host "Invalid path"
        }
    }
}

function PrintHelp {
    param ()
    process {
        Write-Host "Avaible commands: help exit quit proxy list dproxy path"
        Write-Host "Enter link to download file"
    }
}

function Main {
    param ()
    process {
        Write-Host "Enter direct links to download`nType exit or quit to stop"

        $running = $true
        while ($running) {
            $userInput = Read-Host -Prompt ">"
            if ($userInput -eq "exit" -or $userInput -eq "quit") {
                $running = $false
                continue
            }

            if ($userInput -eq "") {
                continue;
            }

            if ($userInput -eq "proxy") {
                $pa = Read-Host -Prompt "Proxy address"
                $pu = $Host.UI.PromptForChoice("Auth", "Proxy uses authorization?", @("&Yes", "&No"), 1)
                [void]( AddProxy -ProxyAddress $pa -ProxyAuth:($pu -eq 0) )
                continue
            }

            if ($userInput -eq "list") {
                PrintProxies
                continue
            }

            if ($userInput -like "dproxy*") {
                $tokens = $userInput.Split(" ")
                if ($tokens.Count -lt 2) {
                    Write-Host "Usage: dproxy <id>"
                    continue
                }
                $pi = [uint]($tokens[1])
                RemoveProxy -Index $pi
                continue
            }

            if ($userInput -like "path*") {
                Write-Host ("Path is {0}" -f $global:savePath)
                $tokens = $userInput.Split(" ")
                if ($tokens.Count -lt 2) {
                    Write-Host "Usage: path <path>"
                    continue
                }
                $p = [string]::Join(" ", $tokens[1..$tokens.Count])
                SetPath -SavePath $p
                continue
            }

            if ($userInput -eq "help" -or $userInput -eq "?") {
                PrintHelp
                continue
            }

            DownloadFile -UrlString $userInput.Trim("/")
        }

        # wait for background download
        Write-Host "Waiting for download(s) to finish"
        # $loader.WaitForDownload()
        while ($loader.IsInProgress) {
            # Write-Host $loader.Progress
            $p = $loader.Progress

            $perc = $p.TotalBytes -eq 0 ? 100 : [int]($p.DownloadedBytes / $p.TotalBytes * 100)
            Write-Progress `
                -Activity ("Downloading {0} file(s)" -f $p.TotalFiles) `
                -Id 0 `
                -Status ("{0}% completed [{1}/{2}]" -f $perc, $p.DownloadedBytes, $p.TotalBytes) `
                -PercentComplete $perc

            $p.Detailed | ForEach-Object {$index = 0} {
                $item = $_
                $index++
                $perc = $item.TotalBytes -eq 0 ? 100 : [int]($item.DownloadedBytes / $item.TotalBytes * 100)
                Write-Progress `
                    -Activity ("Loader {0} | {1} file(s)" -f $item.Name, $item.TotalFiles) `
                    -Id $index `
                    -ParentId 0 `
                    -Status ("{0}% completed [{1}/{2}]" -f $perc, $item.DownloadedBytes, $item.TotalBytes) `
                    -PercentComplete $perc
            }
            Start-Sleep -Seconds 1
        }
        Write-Progress -Id 0 -Activity "All files downloaded" -Completed
        Write-Host "bye"
    }
}

[void]( LoadProxies )
Main
[void]( SaveProxies )