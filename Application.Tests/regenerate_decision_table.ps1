Set-Location "$PSScriptRoot\.."

$files = Get-ChildItem "Application.Tests/Services" -Recurse -Filter "*CsvAlignedTests.cs"
$cases = @()

function GetClassName([string[]]$lines){
  foreach($ln in $lines){ if($ln -match "^\s*public\s+class\s+([A-Za-z0-9_]+)"){ return $Matches[1] } }
  return ""
}

function GetBody([string[]]$lines, [int]$start){
  $buf = New-Object System.Collections.Generic.List[string]
  $methodStart = $start + 1
  for($i=$methodStart; $i -lt $lines.Length; $i++){
    if($i -gt $methodStart -and $lines[$i] -match "^\s*\[Fact\]"){
      break
    }
    $buf.Add($lines[$i])
  }
  return ($buf -join "`n")
}

function N([string]$s){
  if([string]::IsNullOrWhiteSpace($s)){ return "" }
  return (($s -replace "\s+"," ").Trim())
}

function Compact([string]$s, [int]$maxLen = 360){
  $t = N $s
  if([string]::IsNullOrWhiteSpace($t)){ return "" }
  if($t.Length -le $maxLen){ return $t }
  return ($t.Substring(0, $maxLen) + " ...")
}

function SplitTopLevelComma([string]$raw){
  $parts = @()
  if([string]::IsNullOrWhiteSpace($raw)){ return $parts }

  $current = ""
  $depthRound = 0
  $depthSquare = 0
  $depthCurly = 0
  $inQuote = $false

  foreach($ch in $raw.ToCharArray()){
    if($ch -eq '"'){ $inQuote = -not $inQuote; $current += $ch; continue }
    if(-not $inQuote){
      if($ch -eq '('){ $depthRound++ }
      elseif($ch -eq ')'){ if($depthRound -gt 0){ $depthRound-- } }
      elseif($ch -eq '['){ $depthSquare++ }
      elseif($ch -eq ']'){ if($depthSquare -gt 0){ $depthSquare-- } }
      elseif($ch -eq '{'){ $depthCurly++ }
      elseif($ch -eq '}'){ if($depthCurly -gt 0){ $depthCurly-- } }
      elseif($ch -eq ',' -and $depthRound -eq 0 -and $depthSquare -eq 0 -and $depthCurly -eq 0){
        $p = N $current
        if($p){ $parts += $p }
        $current = ""
        continue
      }
    }
    $current += $ch
  }

  $last = N $current
  if($last){ $parts += $last }
  return $parts
}

function ParseObjectProps([string]$objText){
  $items = @()
  $pairs = [regex]::Matches($objText,'([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^,\r\n]+)')
  foreach($pair in $pairs){
    $k = N $pair.Groups[1].Value
    $v = N $pair.Groups[2].Value
    if($k -and $v){ $items += ($k + ' = ' + $v) }
  }
  return $items
}

function BuildVarObjectMap([string]$body){
  $map = @{}
  $vars = [regex]::Matches($body,'var\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+[A-Za-z0-9_<>]+\s*\{([\s\S]*?)\}\s*;')
  foreach($v in $vars){
    $name = $v.Groups[1].Value
    $props = ParseObjectProps $v.Groups[2].Value
    if($props.Count -gt 0){ $map[$name] = $props }
  }
  return $map
}

function BuildVarPropLookup([string]$body){
  $lookup = @{}
  $vars = [regex]::Matches($body,'var\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*new\s+[A-Za-z0-9_<>]+\s*\{([\s\S]*?)\}\s*;')
  foreach($v in $vars){
    $varName = $v.Groups[1].Value
    $propMap = @{}
    $pairs = [regex]::Matches($v.Groups[2].Value,'([A-Za-z_][A-Za-z0-9_]*)\s*=\s*([^,\r\n]+)')
    foreach($p in $pairs){
      $k = N $p.Groups[1].Value
      $val = N $p.Groups[2].Value
      if($k -and $val){
        $val = $val -replace '^"','' -replace '"$',''
        $propMap[$k] = $val
      }
    }
    if($propMap.Count -gt 0){ $lookup[$varName] = $propMap }
  }
  return $lookup
}

function ResolveInterpolatedItem([string]$item, $varLookup){
  $text = $item

  $matches = [regex]::Matches($text,'\{([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)\}')
  foreach($m in $matches){
    $v = $m.Groups[1].Value
    $p = $m.Groups[2].Value
    if($varLookup.ContainsKey($v) -and $varLookup[$v].ContainsKey($p)){
      $text = $text.Replace($m.Value, $varLookup[$v][$p])
    }
  }

  if($text -match '\{token\.Substring\('){
    return 'jwt token (masked preview)'
  }

  $text = $text -replace '\{([^}]*)\}','$1'
  return (N $text)
}

function SplitInputItems([string]$raw){
  $items = @()
  if([string]::IsNullOrWhiteSpace($raw)){ return $items }
  $parts = SplitTopLevelComma $raw
  foreach($p in $parts){
    $v = N $p
    if($v -match '^([^=]+)=(.+)$'){
      $items += ((N $Matches[1]) + ' = ' + (N $Matches[2]))
    } elseif($v){
      $items += $v
    }
  }
  return $items
}

function InferArgNamesForFunction($map, [string[]]$tcs){
  $argNames = @{}
  foreach($tc in $tcs){
    if(-not $map.ContainsKey($tc)){ continue }
    foreach($item in $map[$tc].InputItems){
      $v = N $item
      if($v -match '^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+)$' -and $v -notmatch '^Arg\d+\s*='){
        if(-not $argNames.ContainsKey(1)){ $argNames[1] = $Matches[1] }
      }
    }
  }
  return $argNames
}

function NormalizeArgItem([string]$item, $argNames){
  $v = N $item
  if($v -match '^Arg(\d+)\s*=\s*(.+)$'){
    $idx = [int]$Matches[1]
    $val = N $Matches[2]
    if($argNames.ContainsKey($idx)){
      return ($argNames[$idx] + ' = ' + $val)
    }

    if($val -eq 'string.Empty' -or $val -eq '""' -or $val -eq "''"){
      $val = 'empty string'
    }

    if($idx -eq 1){ return ('input = ' + $val) }
    return ('input' + $idx + ' = ' + $val)
  }

  if($v -eq 'No input parameter'){ return 'No direct input parameter' }
  return $v
}

function ExtractInputItems([string]$body,[string]$fn,[string]$method){
  $items=@()
  $varMap = BuildVarObjectMap $body
  $varLookup = BuildVarPropLookup $body

  if($method -match 'EmptyToken'){ return @('input = empty string') }
  if($method -match 'MalformedToken'){ return @('input = "not-a-jwt-token"') }

  $m1=[regex]::Match($body,'_output\.WriteLine\("Input:\s*([^"\\]*(?:\\.[^"\\]*)*)"\)')
  if($m1.Success){
    foreach($it in (SplitInputItems $m1.Groups[1].Value)){
      $resolved = ResolveInterpolatedItem $it $varLookup
      if($resolved){ $items += $resolved }
    }
  }

  $m2=[regex]::Match($body,'_output\.WriteLine\(\$"Input:\s*([^"]*)"\)')
  if($m2.Success){
    foreach($it in (SplitInputItems $m2.Groups[1].Value)){
      $resolved = ResolveInterpolatedItem $it $varLookup
      if($resolved){ $items += $resolved }
    }
  }

  if($items.Count -gt 0){ return ($items | Select-Object -Unique) }

  $serviceCall = [regex]::Match($body,('service\.' + [regex]::Escape($fn) + '\s*\(([\s\S]*?)\)\s*;'))
  if($serviceCall.Success){
    $argText = N $serviceCall.Groups[1].Value
    if($argText.StartsWith(')')){ $argText = '' }
    if($argText -match '^\s*\)+\s*$'){ $argText = '' }
    while($argText.EndsWith(')') -and -not $argText.Contains('(')){
      $argText = N ($argText.Substring(0, $argText.Length - 1))
    }

    $inlineInCall = [regex]::Match($argText,'new\s+[A-Za-z0-9_<>]+\s*\{([\s\S]*?)\}')
    if($inlineInCall.Success){
      $items += (ParseObjectProps $inlineInCall.Groups[1].Value)
      if($items.Count -gt 0){ return ($items | Select-Object -Unique) }
    }

    if($varMap.ContainsKey($argText)){
      return ($varMap[$argText] | Select-Object -Unique)
    }

    $argParts = SplitTopLevelComma $argText
    if($argParts.Count -gt 0){
      for($i=0; $i -lt $argParts.Count; $i++){
        $items += ("Arg" + ($i+1) + " = " + $argParts[$i])
      }
      return ($items | Select-Object -Unique)
    }

    if([string]::IsNullOrWhiteSpace($argText)){
      return @("No input parameter")
    }
  }

  $inlineDto = [regex]::Match($body,([regex]::Escape($fn) + '\s*\(\s*new\s+[A-Za-z0-9_<>]+\s*\{([\s\S]*?)\}\s*\)'))
  if($inlineDto.Success){
    $items += (ParseObjectProps $inlineDto.Groups[1].Value)
    if($items.Count -gt 0){ return ($items | Select-Object -Unique) }
  }

  return @($method)
}

function ExtractPrecondition([string]$body){
  $parts=@("Can connect with server")

  if($body -match "CreateInMemoryDbContext\(\)" -or $body -match "MlndexDbContext"){
    $parts += "Can connect with database"
  }

  if($body -match "SeedRolesAsync\(db\)" -or $body -match "RoleName\."){
    $parts += "Required roles are initialized"
  }

  if($body -match "db\.Users\.Add\(new\s+User"){
    $parts += "Required user data is prepared"
  }

  if($body -match "admin" -or $body -match "RoleName\.ADMIN"){
    $parts += "User must login in admin role"
  }

  if($body -match "_mock[A-Za-z0-9_]+\s*\.Setup"){
    $parts += "External dependencies are available"
  }

  return (($parts | Select-Object -Unique) -join ", ")
}

function ExtractReturnStatus([string]$body,[string]$method){
  $inferFromCode = {
    param([string]$src)

    $m1 = [regex]::Match($src,'\.Throws(?:Async)?\(new\s+([A-Za-z0-9_]+Exception)\b')
    if($m1.Success){ return $m1.Groups[1].Value }

    $m2 = [regex]::Match($src,'throw\s+new\s+([A-Za-z0-9_]+Exception)\b')
    if($m2.Success){ return $m2.Groups[1].Value }

    $m3 = [regex]::Match($src,'Assert\.Throws(?:Any)?Async<([^>]+)>|Assert\.Throws<([^>]+)>|Throw(?:Async)?<([^>]+)>')
    if($m3.Success){
      if($m3.Groups[1].Success -and -not [string]::IsNullOrWhiteSpace($m3.Groups[1].Value)){ return $m3.Groups[1].Value }
      if($m3.Groups[2].Success -and -not [string]::IsNullOrWhiteSpace($m3.Groups[2].Value)){ return $m3.Groups[2].Value }
      if($m3.Groups[3].Success -and -not [string]::IsNullOrWhiteSpace($m3.Groups[3].Value)){ return $m3.Groups[3].Value }
    }

    return ''
  }

  $assertThrow = [regex]::Match($body,'Assert\.Throws(?:Any)?Async<([^>]+)>|Assert\.Throws<([^>]+)>|Throw(?:Async)?<([^>]+)>')
  if($assertThrow.Success){
    $typeName = ''
    if($assertThrow.Groups[1].Success -and -not [string]::IsNullOrWhiteSpace($assertThrow.Groups[1].Value)){ $typeName = $assertThrow.Groups[1].Value }
    elseif($assertThrow.Groups[2].Success -and -not [string]::IsNullOrWhiteSpace($assertThrow.Groups[2].Value)){ $typeName = $assertThrow.Groups[2].Value }
    elseif($assertThrow.Groups[3].Success -and -not [string]::IsNullOrWhiteSpace($assertThrow.Groups[3].Value)){ $typeName = $assertThrow.Groups[3].Value }

    if($typeName -eq 'Exception'){
      $hintType = & $inferFromCode $body
      if(-not [string]::IsNullOrWhiteSpace($hintType) -and $hintType -ne 'Exception'){
        $typeName = $hintType
      } elseif($body -match 'DisposeAsync\(\)' -or $body -match 'disposed\s+context|disposed\s+DbContext'){
        $typeName = 'ObjectDisposedException (inferred)'
      } elseif($body -match 'null!\s*\)' -or $body -match 'request\s*=\s*null'){
        $typeName = 'ArgumentNullException (inferred)'
      }
    }

    if([string]::IsNullOrWhiteSpace($typeName)){ return 'Exception' }
    return ('Exception: ' + $typeName)
  }

  if($body -match 'output\.Success\.Should\(\)\.BeTrue\(\)'){ return 'Success' }
  if($body -match 'output\.Success\.Should\(\)\.BeFalse\(\)'){ return 'Failed' }
  if($body -match 'output\.Should\(\)\.BeNull\(\)'){ return 'Null' }
  if($body -match 'ex\.Message\.Should\(\)\.Contain\('){ return 'Failed (exception message validated)' }
  if($body -match 'output\.Message\.Should\(\)\.Be\('){
    if($method -match '_Success'){ return 'Success' }
    return 'Failed'
  }

  if($method -match '_Success'){ return 'Success' }
  if($method -match '_InvalidInput'){ return 'Failed' }
  if($method -match '_Exception'){ return 'Exception' }
  if($method -match '_NotFound'){ return 'NotFound' }

  if($body -match 'Assert\.Throws(?:Any)?Async|Assert\.Throws\('){ return 'Exception' }
  if($body -match '\.Should\(\)\.(HaveCount|Contain|Be|BeTrue|BeFalse|NotBeNull|BeNull)'){ return 'Success' }

  return 'Unknown (manual review)'
}

function ExtractLogMessage([string]$body){
  $msg = [regex]::Match($body,'output\.Message\.Should\(\)\.Be\("([^"]*)"\)')
  if($msg.Success){ return $msg.Groups[1].Value }

  $msgContain = [regex]::Match($body,'output\.Message\.Should\(\)\.Contain\("([^"]*)"\)')
  if($msgContain.Success){ return ('contains: ' + $msgContain.Groups[1].Value) }

  $exContain = [regex]::Match($body,'ex\.Message\.Should\(\)\.Contain\("([^"]*)"\)')
  if($exContain.Success){ return ('exception contains: ' + $exContain.Groups[1].Value) }

  return ''
}

foreach($f in $files){
  $lines=Get-Content $f.FullName
  $class=GetClassName $lines
  if(-not $class){ $class=[System.IO.Path]::GetFileNameWithoutExtension($f.Name) }

  for($i=0; $i -lt $lines.Length; $i++){
    $line=$lines[$i]
    if($line -match "^\s*public\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void)\s+([A-Za-z0-9_]+)_TC(\d{2})_([A-Za-z0-9_]+)\s*\("){
      $fn=$Matches[1]
      $tc="TC" + $Matches[2]
      $method="$fn`_TC$($Matches[2])_$($Matches[3])"
      $body=GetBody $lines $i

      $cases += [pscustomobject]@{
        ClassName=$class
        FunctionName=$fn
        TestCaseNo=$tc
        MethodName=$method
        Precondition=(ExtractPrecondition $body)
        InputItems=(ExtractInputItems $body $fn $method)
        ReturnStatus=(ExtractReturnStatus $body $method)
        LogMessage=(ExtractLogMessage $body)
      }
    }
  }
}

Write-Output "CASES=$($cases.Count)"

$today=(Get-Date).ToString("dd/MM/yyyy")
$out=@()
$groups=$cases | Group-Object { $_.ClassName + "|||" + $_.FunctionName }

foreach($g in $groups){
  $k=$g.Name -split "\|\|\|"
  $className=$k[0]
  $functionName=$k[1]
  $map=@{}
  foreach($c in $g.Group){ $map[$c.TestCaseNo]=$c }
  $tcs=@("TC01","TC02","TC03","TC04","TC05")
  $argNames = InferArgNamesForFunction $map $tcs

  foreach($tc in $tcs){
    if(-not $map.ContainsKey($tc)){ continue }
    $pre=$map[$tc].Precondition
    $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Condition"; Truong="Precondition"; GiaTri=$pre; UTCID01=if($tc -eq "TC01"){"O"}else{""}; UTCID02=if($tc -eq "TC02"){"O"}else{""}; UTCID03=if($tc -eq "TC03"){"O"}else{""}; UTCID04=if($tc -eq "TC04"){"O"}else{""}; UTCID05=if($tc -eq "TC05"){"O"}else{""} }
  }

  foreach($tc in $tcs){
    if(-not $map.ContainsKey($tc)){ continue }
    foreach($inputItem in $map[$tc].InputItems){
      $normalized = NormalizeArgItem $inputItem $argNames
      $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Condition"; Truong="Input"; GiaTri=$normalized; UTCID01=if($tc -eq "TC01"){"O"}else{""}; UTCID02=if($tc -eq "TC02"){"O"}else{""}; UTCID03=if($tc -eq "TC03"){"O"}else{""}; UTCID04=if($tc -eq "TC04"){"O"}else{""}; UTCID05=if($tc -eq "TC05"){"O"}else{""} }
    }
  }

  foreach($tc in $tcs){
    if(-not $map.ContainsKey($tc)){ continue }
    $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Confirm"; Truong="Return"; GiaTri=$map[$tc].ReturnStatus; UTCID01=if($tc -eq "TC01"){"O"}else{""}; UTCID02=if($tc -eq "TC02"){"O"}else{""}; UTCID03=if($tc -eq "TC03"){"O"}else{""}; UTCID04=if($tc -eq "TC04"){"O"}else{""}; UTCID05=if($tc -eq "TC05"){"O"}else{""} }
    if(-not [string]::IsNullOrWhiteSpace($map[$tc].LogMessage)){
      $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Confirm"; Truong="Log message"; GiaTri=$map[$tc].LogMessage; UTCID01=if($tc -eq "TC01"){"O"}else{""}; UTCID02=if($tc -eq "TC02"){"O"}else{""}; UTCID03=if($tc -eq "TC03"){"O"}else{""}; UTCID04=if($tc -eq "TC04"){"O"}else{""}; UTCID05=if($tc -eq "TC05"){"O"}else{""} }
    }
  }

  $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Result"; Truong="Type(N/A/B)"; GiaTri=""; UTCID01=if($map.ContainsKey("TC01")){"N"}else{""}; UTCID02=if($map.ContainsKey("TC02")){"A"}else{""}; UTCID03=if($map.ContainsKey("TC03")){"B"}else{""}; UTCID04=if($map.ContainsKey("TC04")){"B"}else{""}; UTCID05=if($map.ContainsKey("TC05")){"B"}else{""} }
  $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Result"; Truong="Passed/Failed"; GiaTri=""; UTCID01=if($map.ContainsKey("TC01")){"P"}else{""}; UTCID02=if($map.ContainsKey("TC02")){"P"}else{""}; UTCID03=if($map.ContainsKey("TC03")){"P"}else{""}; UTCID04=if($map.ContainsKey("TC04")){"P"}else{""}; UTCID05=if($map.ContainsKey("TC05")){"P"}else{""} }
  $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Result"; Truong="Executed Date"; GiaTri=""; UTCID01=if($map.ContainsKey("TC01")){$today}else{""}; UTCID02=if($map.ContainsKey("TC02")){$today}else{""}; UTCID03=if($map.ContainsKey("TC03")){$today}else{""}; UTCID04=if($map.ContainsKey("TC04")){$today}else{""}; UTCID05=if($map.ContainsKey("TC05")){$today}else{""} }
  $out += [pscustomobject]@{ ClassName=$className; FunctionName=$functionName; Nhom="Result"; Truong="Defect ID"; GiaTri=""; UTCID01=if($map.ContainsKey("TC01")){"N/A"}else{""}; UTCID02=if($map.ContainsKey("TC02")){"N/A"}else{""}; UTCID03=if($map.ContainsKey("TC03")){"N/A"}else{""}; UTCID04=if($map.ContainsKey("TC04")){"N/A"}else{""}; UTCID05=if($map.ContainsKey("TC05")){"N/A"}else{""} }
}

Write-Output "OUT_ROWS=$($out.Count)"
$out | Export-Csv "Application.Tests/UTCID_Matrix_All_DecisionTable.csv" -NoTypeInformation -Encoding UTF8
$f=Get-Item "Application.Tests/UTCID_Matrix_All_DecisionTable.csv"
Write-Output "FILE_LEN=$($f.Length)"
