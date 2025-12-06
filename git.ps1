param(
    [Parameter(Mandatory=$false)]
    [string]$StartDate = "2025-11-17",
    
    [Parameter(Mandatory=$false)]
    [string]$AuthorName = "Giulio Ungaretti",
    
    [Parameter(Mandatory=$false)]
    [string]$AuthorEmail = "giulio.ungaretti@gmail.com",
    
    [Parameter(Mandatory=$false)]
    [bool]$DebugMode = $false
)

# Convert to datetime
$baseDate = [datetime]::Parse($StartDate)

# Function to check if date is weekend
function IsWeekend($date) {
    return $date.DayOfWeek -eq [System.DayOfWeek]::Saturday -or $date.DayOfWeek -eq [System.DayOfWeek]::Sunday
}


function GetCommitTime {
    param(
        [Parameter(Mandatory=$true)]
        [datetime]$date,
        
        [Parameter(Mandatory=$false)]
        [int]$preferredHour = -1,
        
        [Parameter(Mandatory=$false)]
        [int]$preferredMinute = -1
    )
    
    $base = $date.Date  # midnight same day

    if ($preferredHour -ge 0) {
        $hour = $preferredHour
    }
    elseif (IsWeekend $date) {
        $hour = Get-Random -Minimum 7 -Maximum 23
    }
    else {
        $timeSlot = Get-Random -Minimum 0 -Maximum 2
        if ($timeSlot -eq 0) {
            $hour = Get-Random -Minimum 6 -Maximum 8
        } else {
            $hour = Get-Random -Minimum 18 -Maximum 24
        }
    }

    if ($preferredMinute -ge 0) {
        $minute = $preferredMinute
    } else {
        $minute = Get-Random -Minimum 0 -Maximum 60
    }

    $second = Get-Random -Minimum 0 -Maximum 60
    $result = $base.AddHours($hour).AddMinutes($minute).AddSeconds($second)
    if ($DebugMode) {
        Write-Host "  [DEBUG] GetCommitTime: hour=$hour, minute=$minute, second=$second -> $($result.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor DarkGray
    }
    return $result
}

# helper function to set the committer identity using env variables
function SetCommitterIdentity($name, $email) {
    $env:GIT_AUTHOR_NAME = $name
    $env:GIT_AUTHOR_EMAIL = $email
    $env:GIT_COMMITTER_NAME = $name
    $env:GIT_COMMITTER_EMAIL = $email
}

# Function to create a commit with specific date
function CreateCommit($message, $date, $files) {
    Write-Host "Creating commit: $message" -ForegroundColor Cyan
    Write-Host "  Date: $date" -ForegroundColor Gray
    
    # Stage files
    if ($files -and $files.Count -gt 0) {
        foreach ($file in $files) {
            if (Test-Path $file) {
                git add $file
                Write-Host "  Added: $file" -ForegroundColor Green
            }
            else {
                Write-Host "  Warning: File not found - $file" -ForegroundColor Yellow
            }
        }
    }
    else {
        git add -A
    }
    
    # Format date for git (ISO 8601 format) - use invariant culture to ensure colons
    $gitDate = $date.ToString("yyyy-MM-ddTHH:mm:ss", [System.Globalization.CultureInfo]::InvariantCulture)
    if ($DebugMode) {
        Write-Host "  [DEBUG] Git date string: $gitDate" -ForegroundColor DarkGray
    }
    
    # Set environment variables
    $env:GIT_AUTHOR_DATE = $gitDate
    $env:GIT_COMMITTER_DATE = $gitDate
    $env:GIT_AUTHOR_NAME = $AuthorName
    $env:GIT_AUTHOR_EMAIL = $AuthorEmail
    $env:GIT_COMMITTER_NAME = $AuthorName
    $env:GIT_COMMITTER_EMAIL = $AuthorEmail
    
    git commit -m $message
    
    # Verify the commit date
    $actualDate = git log -1 --format="%ai"
    if ($DebugMode) {
        Write-Host "  [DEBUG] Actual commit date: $actualDate" -ForegroundColor DarkGray
    }
    
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Magenta
Write-Host "net pods Git History Creation Script" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "Starting date: $StartDate" -ForegroundColor Yellow
Write-Host "Author: $AuthorName <$AuthorEmail>" -ForegroundColor Yellow
SetCommitterIdentity $AuthorName $AuthorEmail
Write-Host ""
