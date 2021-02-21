if (Get-Service CleanupSvc -ErrorAction Ignore) {
	Write-Error 'Service "CleanupSvc" already exists. Nothing to do.'
} else {
	$bin = Get-Item ./CleanupSvc.exe -ErrorAction Stop
	New-Service -Name CleanupSvc -Description 'Remove temporary files of a certain age' -StartupType Automatic -BinaryPathName $bin.FullName -ErrorAction Stop
	Start-Service CleanupSvc -ErrorAction Stop
	Write-Host 'Service installed' -ForegroundColor Green
	Write-Host 'To see the service log, type "Get-EventLog -LogName Application -Source CleanupSvc | select TimeGenerated,Message | Format-List"'
}