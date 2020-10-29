# dotsource and call
# Start-Analysis

function Start-Analysis {
	[CmdletBinding()]
	param (

	)
	Get-CurrentOmnisharpLogFile -Verbose -Path "~/AppData/Roaming/Code/"`
	| Get-Content -Wait -Tail 100 `
	| ConvertTo-FileChunks -StartPattern "^{" -EndPattern "^}" -FunctionToApplyToChunk { $args | ConvertFrom-Json } `
	| Select-Requests `
	| Format-Table `
			@{n = "Command"; e = { $_.Command }; width = 20}, `
			@{n = "Time"; e = { $_.RequestTime.ToString("T") }; width = 30}, `
			@{n = "DurationSec"; e = { $color = 0; if ($_.ResponseDurationSeconds -gt 1){$color = 35; } $e = [char]27; "$e[${color}m$($_.ResponseDurationSeconds.ToString("0.##"))${e}[0m"; } }, `
			@{n = "Pending"; e = { $_.PendingRequests } }, `
			@{n = "Details"; e = { $_.Details }; width = 20 }
}

function Get-CurrentOmnisharpLogFile{
	[CmdletBinding()]
	param (
		$Path = "~/AppData/Roaming/Code*" # defaults to Code* which includes Code-Insiders
	)
	Get-Item $Path `
	| Get-ChildItem -Recurse -Filter "*OmniSharp*" `
	| Sort-Object -Property LastWriteTime -Descending `
	| Select-Object -First 1 `
	| %{write-Verbose $_; $_}
}

function ConvertTo-FileChunks {
	[CmdletBinding()]
	param (
		[Parameter(Mandatory, ValueFromPipeline)]
		$InputObject,
		[string]$StartPattern,
		[string]$EndPattern,
		[scriptblock]$FunctionToApplyToChunk,
		[int]$First
	)
	

	begin {
		$chunk = ""
		$withinChunk = $false
		$processedChunks = 0
	}
	
	process {
		if ([regex]::Matches($InputObject, $StartPattern)) {
			Write-Verbose "start pattern matches"
			if ($chunk) {
				Write-Verbose "calling function on chunk"
				$FunctionToApplyToChunk.Invoke($chunk)
				$chunk = ""
				$processedChunks ++
			}
			$withinChunk = $true
		}
		if ($withinChunk) {
			$chunk += $InputObject + [System.Environment]::NewLine
		}
		if ($withinChunk -and [regex]::Matches($InputObject, $EndPattern)) {
			Write-Verbose "end pattern matches"
			if ($chunk) {
				Write-Verbose "calling function on chunk"
				$FunctionToApplyToChunk.Invoke($chunk)
				$chunk = ""
				$processedChunks ++
			}
			$withinChunk = $false
		}
		if ($First -And ($First -gt 0) -and ($processedChunks -ge $First)) {
			Write-Verbose "breaking after $processedChunks chunks"
			break;
		}
	}
	
	end {
		if ($chunk) {
			Write-Verbose "calling function on chunk"
			$FunctionToApplyToChunk.Invoke($chunk)
			$chunk = ""
			$processedChunks ++
		}
	}
}

function Select-Requests {
	[CmdletBinding()]
	param (
		[Parameter(Mandatory, ValueFromPipeline)]
		$InputObject
	)
	begin {
		$pendingRequests = @{}
	}
	process {
		if ($InputObject.Type -eq "Request") {
			Add-Member -InputObject $InputObject -Type NoteProperty -Name "RequestTime" -Value (Get-Date)
			$pendingRequests[$InputObject.Seq] = $InputObject
		}
		else {
			if (-not $InputObject.Request_seq) {
				$InputObject | Out-String | Write-Host -ForegroundColor Yellow
			}
			$request = $pendingRequests[$InputObject.Request_seq]
			
			$request |out-string| write-Verbose

			$pendingRequests.Remove($InputObject.Request_seq)
			$outputObject = $InputObject
			Add-Member -InputObject $outputObject -Type NoteProperty -Name "PendingRequests" -Value $pendingRequests.count
			Add-Member -InputObject $outputObject -Type NoteProperty -Name "RequestTime" -Value $request.RequestTime
			Add-Member -InputObject $outputObject -Type NoteProperty -Name "ResponseDurationSeconds" -Value ( (Get-Date).Subtract($request.RequestTime).TotalSeconds )

			$details = ""

			if ($request.Command -match "codecheck"){
				$details = "full project scan"
				if ($request.Arguments.FileName){
					$details = "single file scan"
				}
			}

			Add-Member -InputObject $outputObject -Type NoteProperty -Name "Details" -Value $details

			$outputObject 
		}
	}
	end {
	}
}