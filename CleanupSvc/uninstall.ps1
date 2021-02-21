if (Get-Service CleanupSvc -ErrorAction Ignore) {
	Stop-Service CleanupSvc -ErrorAction Stop
	Write-Host 'Waiting for the service to stop'
	(Get-Service CleanupSvc).WaitForStatus('Stopped', (New-TimeSpan -Seconds 5))
	if ((Get-Service CleanupSvc).Status -ne 'Stopped') {
		Write-Error "The service didn't stop in a timely fashion"
	}
	sc.exe delete CleanupSvc
} else {
	Write-Host 'The service does not exist'
}