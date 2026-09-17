[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$checks = [System.Collections.Generic.List[object]]::new()
$tolerance = 0.001

$authoritativeSourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\aks74ucovert\source\aks-74u_extract\source\AKS74U Sketchfab.fbx'
$expectedSourceHash = '2066A418F4C12C65C3D2D243A6981AFDB7A1B285C25082FBF72CFF145CC48B26'
$standaloneSourcePath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\attachments\pbs04\source\pbs04_suppressor.fbx'
$modelDocPath = Join-Path $repoRoot 'game\Assets\addons\lifepunch\lpweapons\attachments\pbs04\pbs04_suppressor.vmdl'
$standaloneSourceAsset = 'addons/lifepunch/lpweapons/attachments/pbs04/source/pbs04_suppressor.fbx'
$blenderPath = 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe'

# Normalization maps the authored mesh-local -Z bore direction to product +X,
# centers the bore on Y/Z, and places the first physical rear-facing annular
# shoulder at X=0. The open threaded entrance intentionally remains behind it.
$expectedBounds = [pscustomobject]@{
	min = @(-0.149798200, -0.163186595, -0.163186640)
	max = @(1.594735816, 0.163186595, 0.163186565)
}
$expectedShoulderArea = 0.051455400418
$expectedTopologyHash = 'E16C212B0CAEB23E20CE58406A83AFEDE9B43E97DEEF2C4FB894D8902A6672F7'

function Add-Check {
	param(
		[Parameter(Mandatory)] [string] $Name,
		[Parameter(Mandatory)] [bool] $Passed,
		[Parameter(Mandatory)] [string] $Evidence
	)

	$checks.Add([pscustomobject]@{
		name = $Name
		passed = $Passed
		evidence = $Evidence
	})
}

function Test-Near {
	param([double] $Actual, [double] $Expected, [double] $Allowed = $tolerance)
	return [Math]::Abs($Actual - $Expected) -le $Allowed
}

function Test-NormalizedMeasurement {
	param([Parameter(Mandatory)] $Measurement)

	if ($Measurement.meshCount -ne 1 -or $Measurement.vertexCount -ne 645 -or
		$Measurement.polygonCount -ne 644 -or $Measurement.topologySha256 -cne $expectedTopologyHash -or
		-not [bool]$Measurement.windingProbePassed) {
		return $false
	}
	for ($axis = 0; $axis -lt 3; $axis++) {
		if (-not (Test-Near ([double]$Measurement.bounds.min[$axis]) $expectedBounds.min[$axis])) { return $false }
		if (-not (Test-Near ([double]$Measurement.bounds.max[$axis]) $expectedBounds.max[$axis])) { return $false }
	}

	return $Measurement.shoulderFaceCount -gt 0 -and
		[double]$Measurement.shoulderRearNormalX -lt -0.9 -and
		(Test-Near ([double]$Measurement.shoulderArea) $expectedShoulderArea 0.001) -and
		$Measurement.borePointCount -eq 75 -and
		[double]$Measurement.boreAxis[0] -gt 0.9999 -and
		[Math]::Abs([double]$Measurement.boreAxis[1]) -le 0.001 -and
		[Math]::Abs([double]$Measurement.boreAxis[2]) -le 0.01 -and
		[Math]::Abs([double]$Measurement.boreYIntercept) -le 0.001 -and
		[Math]::Abs([double]$Measurement.boreZIntercept) -le 0.001 -and
		(Test-Near ([double]$Measurement.boreRadiusMin) 0.034857087) -and
		(Test-Near ([double]$Measurement.boreRadiusMax) 0.036542389)
}

function Invoke-BlenderMeasurement {
	param([Parameter(Mandatory)] [string] $Path)

	$priorInput = [Environment]::GetEnvironmentVariable('DXRP_PBS_GEOMETRY_INPUT', 'Process')
	try {
		[Environment]::SetEnvironmentVariable('DXRP_PBS_GEOMETRY_INPUT', $Path, 'Process')
		$python = @'
import bpy, hashlib, json, os
from mathutils import Matrix, Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.environ["DXRP_PBS_GEOMETRY_INPUT"])
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
points = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
mins = [min(point[i] for point in points) for i in range(3)]
maxs = [max(point[i] for point in points) for i in range(3)]
def quantized(vector):
    return ",".join(str(int(round(float(component) * 100000.0))) for component in vector)
topology_vertices = sorted(quantized(point) for point in points)
topology_polygons = []
def oriented_cycle(values):
    rotations = [values[index:] + values[:index] for index in range(len(values))]
    return ";".join(min(rotations))
for obj in meshes:
    world_vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    for polygon in obj.data.polygons:
        coordinates = [quantized(world_vertices[index]) for index in polygon.vertices]
        topology_polygons.append(oriented_cycle(coordinates))
topology_payload = "V|" + "|".join(topology_vertices) + "|P|" + "|".join(sorted(topology_polygons))
bore = [point for point in points if 0.034 <= (point.y * point.y + point.z * point.z) ** 0.5 <= 0.037]
bore_center = sum(bore, Vector()) / len(bore) if bore else Vector()
covariance = Matrix(((0.0, 0.0, 0.0), (0.0, 0.0, 0.0), (0.0, 0.0, 0.0)))
for point in bore:
    delta = point - bore_center
    for row in range(3):
        for column in range(3):
            covariance[row][column] += delta[row] * delta[column]
bore_axis = Vector((1.0, 0.0, 0.0))
for _ in range(50):
    candidate = covariance @ bore_axis
    if candidate.length <= 1e-12:
        bore_axis = Vector()
        break
    bore_axis = candidate.normalized()
if bore_axis.x < 0.0:
    bore_axis.negate()
sum_x = sum(point.x for point in bore)
sum_xx = sum(point.x * point.x for point in bore)
denominator = len(bore) * sum_xx - sum_x * sum_x
def regression(component):
    if len(bore) < 2 or abs(denominator) <= 1e-12:
        return 0.0, 1000000000.0
    sum_y = sum(point[component] for point in bore)
    sum_xy = sum(point.x * point[component] for point in bore)
    slope = (len(bore) * sum_xy - sum_x * sum_y) / denominator
    return slope, (sum_y - slope * sum_x) / len(bore)
bore_y_slope, bore_y_intercept = regression(1)
bore_z_slope, bore_z_intercept = regression(2)
bore_radii = [(point.y * point.y + point.z * point.z) ** 0.5 for point in bore] or [0.0]
shoulders = []
for obj in meshes:
    normal_matrix = obj.matrix_world.inverted_safe().transposed().to_3x3()
    for polygon in obj.data.polygons:
        vertices = [obj.matrix_world @ obj.data.vertices[index].co for index in polygon.vertices]
        normal = (normal_matrix @ polygon.normal).normalized()
        if vertices and max(abs(vertex.x) for vertex in vertices) <= 0.0001 and normal.x < -0.9:
            origin = vertices[0]
            area = 0.0
            for index in range(1, len(vertices) - 1):
                area += ((vertices[index] - origin).cross(vertices[index + 1] - origin)).length * 0.5
            shoulders.append({"area": area, "normal_x": normal.x})
payload = {
    "meshCount": len(meshes),
    "meshNames": sorted(obj.name for obj in meshes),
    "vertexCount": sum(len(obj.data.vertices) for obj in meshes),
    "polygonCount": sum(len(obj.data.polygons) for obj in meshes),
    "topologySha256": hashlib.sha256(topology_payload.encode("utf-8")).hexdigest().upper(),
    "windingProbePassed": oriented_cycle(["0", "1", "2"]) != oriented_cycle(["2", "1", "0"]),
    "bounds": {"min": mins, "max": maxs},
    "shoulderFaceCount": len(shoulders),
    "shoulderArea": sum(item["area"] for item in shoulders),
    "shoulderRearNormalX": max((item["normal_x"] for item in shoulders), default=0.0),
    "borePointCount": len(bore),
    "boreAxis": list(bore_axis),
    "boreYIntercept": bore_y_intercept,
    "boreZIntercept": bore_z_intercept,
    "boreRadiusMin": min(bore_radii),
    "boreRadiusMax": max(bore_radii),
}
print("DXRP_PBS_GEOMETRY=" + json.dumps(payload, sort_keys=True, separators=(",", ":")))
'@
		$output = @(& $blenderPath --background --factory-startup --python-expr $python 2>&1)
		$exitCode = $LASTEXITCODE
		$marker = @($output | Where-Object { $_ -like 'DXRP_PBS_GEOMETRY=*' })
		if ($exitCode -ne 0 -or $marker.Count -ne 1) {
			throw "Blender PBS probe failed: exit=$exitCode markers=$($marker.Count)."
		}
		return ($marker[0].Substring('DXRP_PBS_GEOMETRY='.Length) | ConvertFrom-Json -Depth 8)
	}
	finally {
		[Environment]::SetEnvironmentVariable('DXRP_PBS_GEOMETRY_INPUT', $priorInput, 'Process')
	}
}

$sourceExists = Test-Path -LiteralPath $authoritativeSourcePath -PathType Leaf
$sourceHash = if ($sourceExists) { (Get-FileHash -Algorithm SHA256 -LiteralPath $authoritativeSourcePath).Hash } else { '' }
Add-Check 'authoritative_original_source_pin' ($sourceHash -ceq $expectedSourceHash) "expected=$expectedSourceHash actual=$sourceHash"

$standaloneExists = Test-Path -LiteralPath $standaloneSourcePath -PathType Leaf
Add-Check 'standalone_pbs_source_exists' $standaloneExists $standaloneSourcePath
if ($standaloneExists) {
	$blenderExists = Test-Path -LiteralPath $blenderPath -PathType Leaf
	Add-Check 'blender_geometry_sensor_available' $blenderExists $blenderPath
	if ($blenderExists) {
		$measurement = Invoke-BlenderMeasurement $standaloneSourcePath
		Add-Check 'standalone_pbs_has_source_derived_topology' ($measurement.meshCount -eq 1 -and $measurement.meshNames[0] -ceq 'pbs04_suppressor' -and $measurement.vertexCount -eq 645 -and $measurement.polygonCount -eq 644 -and $measurement.topologySha256 -ceq $expectedTopologyHash) ($measurement | ConvertTo-Json -Compress -Depth 4)
		Add-Check 'standalone_pbs_shoulder_origin_and_forward_axis' (Test-NormalizedMeasurement $measurement) ($measurement | ConvertTo-Json -Compress -Depth 4)
	}
}

$modelDocExists = Test-Path -LiteralPath $modelDocPath -PathType Leaf
Add-Check 'standalone_pbs_modeldoc_exists' $modelDocExists $modelDocPath
if ($modelDocExists) {
	$modelText = Get-Content -LiteralPath $modelDocPath -Raw
	$sourceMatches = [regex]::Matches($modelText, '(?m)^\s*filename\s*=\s*"(?<value>[^"]+)"\s*$')
	$scaleMatches = [regex]::Matches($modelText, '(?m)^\s*import_scale\s*=\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*$')
	$translationIsIdentity = $modelText -match '(?m)^\s*import_translation\s*=\s*\[\s*0(?:\.0+)?,\s*0(?:\.0+)?,\s*0(?:\.0+)?\s*\]\s*$'
	$rotationIsIdentity = $modelText -match '(?m)^\s*import_rotation\s*=\s*\[\s*0(?:\.0+)?,\s*0(?:\.0+)?,\s*0(?:\.0+)?\s*\]\s*$'
	$importScale = if ($scaleMatches.Count -eq 1) { [double]::Parse($scaleMatches[0].Groups['value'].Value, [Globalization.CultureInfo]::InvariantCulture) } else { 0.0 }
	$usesStandaloneSource = $sourceMatches.Count -eq 1 -and $sourceMatches[0].Groups['value'].Value -ceq $standaloneSourceAsset
	Add-Check 'pbs_modeldoc_uses_only_normalized_source' $usesStandaloneSource "expected=$standaloneSourceAsset"
	Add-Check 'pbs_modeldoc_preserves_authored_geometry_seam' ($translationIsIdentity -and $rotationIsIdentity -and $scaleMatches.Count -eq 1 -and $importScale -gt 0.0) "identityTranslation=$translationIsIdentity identityRotation=$rotationIsIdentity scale=$importScale"
}

$validFixture = [pscustomobject]@{
	meshCount = 1
	vertexCount = 645
	polygonCount = 644
	topologySha256 = $expectedTopologyHash
	windingProbePassed = $true
	bounds = [pscustomobject]@{ min = @($expectedBounds.min); max = @($expectedBounds.max) }
	shoulderFaceCount = 1
	shoulderArea = $expectedShoulderArea
	shoulderRearNormalX = -1.0
	borePointCount = 75
	boreAxis = @(0.999995470, 0.0, -0.003030930)
	boreYIntercept = 0.0
	boreZIntercept = 0.000266067
	boreRadiusMin = 0.034857087
	boreRadiusMax = 0.036542389
}
$shiftedFixture = $validFixture | ConvertTo-Json -Depth 5 | ConvertFrom-Json
$shiftedFixture.bounds.min[0] = [double]$shiftedFixture.bounds.min[0] + 0.2
$shiftedFixture.bounds.max[0] = [double]$shiftedFixture.bounds.max[0] + 0.2
$reversedFixture = $validFixture | ConvertTo-Json -Depth 5 | ConvertFrom-Json
$reversedFixture.bounds.min[0] = -$expectedBounds.max[0]
$reversedFixture.bounds.max[0] = -$expectedBounds.min[0]
$reversedFixture.shoulderRearNormalX = 1.0
$reversedFixture.boreAxis[0] = -1.0
$offCenterFixture = $validFixture | ConvertTo-Json -Depth 5 | ConvertFrom-Json
$offCenterFixture.boreZIntercept = 0.02
$windingBlindFixture = $validFixture | ConvertTo-Json -Depth 5 | ConvertFrom-Json
$windingBlindFixture.windingProbePassed = $false

Add-Check 'positive_control_accepts_measured_normalization' (Test-NormalizedMeasurement $validFixture) 'measured shoulder-origin fixture'
Add-Check 'negative_control_rejects_pivot_displacement' (-not (Test-NormalizedMeasurement $shiftedFixture)) 'in-memory +0.2 X pivot shift'
Add-Check 'negative_control_rejects_axis_reversal' (-not (Test-NormalizedMeasurement $reversedFixture)) 'in-memory X reversal and shoulder-normal flip'
Add-Check 'negative_control_rejects_off_center_bore' (-not (Test-NormalizedMeasurement $offCenterFixture)) 'in-memory bore intercept shift'
Add-Check 'negative_control_rejects_winding_blind_digest' (-not (Test-NormalizedMeasurement $windingBlindFixture)) 'digest implementation must distinguish forward and reversed polygon order'

$failures = @($checks | Where-Object { -not $_.passed })
[pscustomobject]@{
	contract = 'PBS-04 standalone attachment geometry'
	status = if ($failures.Count -eq 0) { 'PASS' } else { 'FAIL' }
	expectedBounds = $expectedBounds
	expectedShoulderArea = $expectedShoulderArea
	checks = $checks
	failureCount = $failures.Count
} | ConvertTo-Json -Depth 8

if ($failures.Count -gt 0) { exit 1 }
exit 0
