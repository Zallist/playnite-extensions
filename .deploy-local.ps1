param(
	[Parameter()]
    [ValidateNotNullOrEmpty()]
	[string]$destination=$(throw "Parameter -destination is mandatory, please provide a value.")
) 

function copyAllFiles($srcDir, $dstDir) {
	$srcDir = $srcDir.Trim()
	$dstDir = $dstDir.Trim()

	if (Test-Path $dstDir) {
		Remove-Item -Recurse -Force $dstDir
	}

	Copy-Item -Path $srcDir -Destination $dstDir -Recurse
}


function copyAllOutTo($dstDir) {
	echo "Copying all out to $dstDir"

	# for every Debug folder
	Get-ChildItem . -Recurse -Directory -Filter Debug | ForEach-Object {
		$path = $_.FullName

		# if parent is not bin skip
		if ($_.Parent.Name -ne "bin") { 
			return 
		}

		# get the extension from the extension.yaml file
		$yamlFile = Join-Path $path "extension.yaml"

		if (!(Test-Path $yamlFile)) { 
			echo "No file found at $yamlFile"
			return 
		}

		$yaml = Get-Content $yamlFile -Raw

		# Match the regex 'ID: (.*)' to get the ID
		if ($yaml -match "ID\W+(.*)") {
			$id = $matches.1
			$copyTo = Join-Path $dstDir $id

			copyAllFiles $_.FullName $copyTo
		}
		else {
			echo "No ID found in $yamlFile"
			return
		}

	}
	
	echo "Copied all out to $dstDir"
}

$destination = [System.Environment]::ExpandEnvironmentVariables($destination)
copyAllOutTo $destination