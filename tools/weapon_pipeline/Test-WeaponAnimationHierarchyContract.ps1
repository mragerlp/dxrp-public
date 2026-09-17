[CmdletBinding()]
param(
	[string] $M1911PrefabOverride = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$failures = [System.Collections.Generic.List[string]]::new()
$rows = [System.Collections.Generic.List[object]]::new()

function Read-Prefab( [string] $RelativePath )
{
	$path = if ( [System.IO.Path]::IsPathRooted( $RelativePath ) )
	{
		$RelativePath
	}
	else
	{
		Join-Path $repoRoot $RelativePath
	}
	if ( !(Test-Path -LiteralPath $path -PathType Leaf) )
	{
		$failures.Add( "Missing required prefab: $RelativePath" )
		return $null
	}

	try
	{
		return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
	}
	catch
	{
		$failures.Add( "Invalid prefab JSON in $RelativePath`: $($_.Exception.Message)" )
		return $null
	}
}

function Get-ObjectProperty( [object] $Object, [string] $Name )
{
	if ( $null -eq $Object )
	{
		return $null
	}

	$property = $Object.PSObject.Properties[$Name]
	if ( $null -eq $property )
	{
		return $null
	}

	return $property.Value
}

function Find-NodeWithParent( [object] $Node, [string] $Guid, [object] $Parent = $null )
{
	if ( $null -eq $Node )
	{
		return $null
	}
	if ( $Node.__guid -eq $Guid )
	{
		return [ordered]@{
			Node = $Node
			Parent = $Parent
		}
	}

	foreach ( $child in @($Node.Children) )
	{
		$match = Find-NodeWithParent $child $Guid $Node
		if ( $null -ne $match )
		{
			return $match
		}
	}

	return $null
}

function Find-ComponentsByType( [object] $Node, [string] $TypeName )
{
	if ( $null -eq $Node )
	{
		return
	}

	foreach ( $component in @($Node.Components) )
	{
		if ( (Get-ObjectProperty $component '__type') -eq $TypeName )
		{
			$component
		}
	}

	foreach ( $child in @($Node.Children) )
	{
		Find-ComponentsByType $child $TypeName
	}
}

$contracts = @(
	[ordered]@{
		Weapon = 'AK-47'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\ak47\equipment\vm_ak47\vm_ak47.prefab'
		Guid = '773f2f3c-53ab-4a96-bfbc-8d64ef4e0450'
		Name = 'ak47_mesh'
		Parent = 'weapon_root'
		ParentGuid = 'e7d10f28-79cd-4b00-b5ec-6ce242c66e91'
		ComponentGuid = 'a32f8062-802a-4118-8b3b-41cf43097c6e'
		Model = 'addons/lifepunch/lpweapons/ak47/models/lifepunch/ak47/w_ak47/w_ak47.vmdl'
		Position = '4.284633,0.1895416,-2.894256'
		Rotation = '0.03345421,-0.0000000014623299,-0.99944025,-0.00000004368692'
		Scale = '0.84991359,0.84991359,0.84991359'
	}
	[ordered]@{
		Weapon = 'AKS-74U'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\vm_aks74u\vm_aks74u.prefab'
		Guid = '5c1d688f-d6c7-4fbe-9c11-2c5f536900ef'
		Name = 'aks74u_body'
		Parent = 'weapon_root_children'
		ParentGuid = 'fdea3396-48d1-4e87-b977-e9676173698d'
		ComponentGuid = '6dbed7b5-5dc5-4514-a59d-a91413c92223'
		Model = 'addons/lifepunch/lpweapons/aks74u/aks74u_body_minus_bolt.vmdl'
		Position = '1.0136986,0.13541961,7.1526102'
		Rotation = '0,0,0,1'
		Scale = '0.89572925,0.89572925,0.89572925'
	}
	[ordered]@{
		Weapon = 'AKS-74U'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\vm_aks74u\vm_aks74u.prefab'
		Guid = 'b583651e-982a-57d4-bcbb-564696b46a23'
		Name = 'aks74u_bolt'
		Parent = 'bolt'
		ParentGuid = '577a4d37-a52a-4363-96a3-a8ac90106db2'
		ComponentGuid = 'a6151211-0429-5ec4-8a36-acbcc6b40888'
		Model = 'addons/lifepunch/lpweapons/aks74u/aks74u_bolt.vmdl'
		Position = '-3.0849904,0.13541761,4.4021212'
		Rotation = '0,0,0,1'
		Scale = '0.89572925,0.89572925,0.89572925'
	}
	[ordered]@{
		Weapon = 'AKS-74U'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\vm_aks74u\vm_aks74u.prefab'
		Guid = '71078152-f9e9-4f76-9003-fbab317b6f57'
		Name = 'aks74u_magazine'
		Parent = 'magazine'
		ParentGuid = 'bc5a4213-530c-4acf-8687-f6c048531fde'
		ComponentGuid = '25629190-9d09-4165-8143-5801df1c1d86'
		Model = 'addons/lifepunch/lpweapons/aks74u/aks74u_mag.vmdl'
		Position = '-6.74232687,0.13541861,-4.00666286'
		Rotation = '0,-0.70710628,0,0.70710728'
		Scale = '0.89572925,0.89572925,0.89572925'
	}
	[ordered]@{
		Weapon = 'Desert Eagle'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\vm_desert_eagle\vm_desert_eagle.prefab'
		Guid = '49ebd323-d52a-451b-ac1f-a37d51a13180'
		Name = 'desert_eagle_body'
		Parent = 'weapon_root_children'
		ParentGuid = '13f4cc37-6ade-4a6b-b670-7a4d50663302'
		ComponentGuid = '87e02586-4d69-4829-a45b-11be950a16a9'
		Model = 'addons/lifepunch/lpweapons/deserteagle/desert_eagle_body.vmdl'
		Position = '2.8425531,-0.029227138,-3.1230555'
		Rotation = '0,0,0,1'
		Scale = '1,1,1'
	}
	[ordered]@{
		Weapon = 'Desert Eagle'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\vm_desert_eagle\vm_desert_eagle.prefab'
		Guid = '832b4f45-d26e-5839-9d88-0c0481a31215'
		Name = 'desert_eagle_magazine_bind_wrapper'
		Parent = 'magazine'
		ParentGuid = 'dff4e566-fa52-4130-bb5d-26c3e951206a'
		ExpectedChildGuid = '07c7131b-503b-47c3-8780-c3fe309c78d8'
		Position = '-0.81244256471166221,0,-0.15842932801374754'
		Rotation = '0,-0.79863533644524842,0,0.60181525352967336'
		Scale = '1.0000000000001106,1,1.0000000000001104'
	}
	[ordered]@{
		Weapon = 'Desert Eagle'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\vm_desert_eagle\vm_desert_eagle.prefab'
		Guid = '1f054dc6-ab48-47b8-93e3-88144e5da88a'
		Name = 'desert_eagle_slide'
		Parent = 'slide'
		ParentGuid = '014fa5da-2828-4854-8a20-b808bf7b469d'
		ComponentGuid = 'b312a440-4eee-40b3-a3b6-149dac8479b9'
		Model = 'addons/lifepunch/lpweapons/deserteagle/desert_eagle_slide.vmdl'
		Position = '1.5202713,-0.029129624,-4.7529078'
		Rotation = '0,0,0,1'
		Scale = '1,1,1'
	}
	[ordered]@{
		Weapon = 'Desert Eagle'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\vm_desert_eagle\vm_desert_eagle.prefab'
		Guid = '07c7131b-503b-47c3-8780-c3fe309c78d8'
		Name = 'desert_eagle_magazine'
		Parent = 'desert_eagle_magazine_bind_wrapper'
		ParentGuid = '832b4f45-d26e-5839-9d88-0c0481a31215'
		ComponentGuid = 'f5a510e8-628a-4dd6-a660-06180a870ef2'
		Model = 'addons/lifepunch/lpweapons/deserteagle/desert_eagle_magazine.vmdl'
		Position = '2.8425531,-0.029227138,-3.1230555'
		Rotation = '0,0,0,1'
		Scale = '1,1,1'
	}
	[ordered]@{
		Weapon = 'M870'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\m870\equipment\vm_m870\vm_m870.prefab'
		Guid = 'eb46deef-8f8a-444d-8e0c-bfdd8709b969'
		Name = 'm870_body'
		Parent = 'weapon_root_children'
		ParentGuid = '4cc14110-8cd5-49c7-88ab-075471d0ad15'
		ComponentGuid = '20b38c12-b055-4434-93a5-65b2d0f2039a'
		Model = 'addons/lifepunch/lpweapons/m870/models/m870_body.vmdl'
		Position = '7.610142,0.556946,4.144161'
		Rotation = '0,0,0,1'
		Scale = '1.018760,1.018760,1.018760'
	}
	[ordered]@{
		Weapon = 'M870'
		Prefab = 'game\Assets\addons\lifepunch\lpweapons\m870\equipment\vm_m870\vm_m870.prefab'
		Guid = 'f67b6b3a-5cf9-4346-87fc-2643faf53ea3'
		Name = 'm870_pump'
		Parent = 'weapon_root_children'
		ParentGuid = '4cc14110-8cd5-49c7-88ab-075471d0ad15'
		ComponentGuid = '5187b64e-db32-494d-abe2-7ce324af9d04'
		Model = 'addons/lifepunch/lpweapons/m870/models/m870_pump.vmdl'
		Position = '7.610142,0.556946,4.144161'
		Rotation = '0,0,0,1'
		Scale = '1.018760,1.018760,1.018760'
	}
)

$prefabs = @{}
foreach ( $contract in $contracts )
{
	if ( !$prefabs.ContainsKey( $contract.Prefab ) )
	{
		$prefabs[$contract.Prefab] = Read-Prefab $contract.Prefab
	}
	$prefab = $prefabs[$contract.Prefab]
	if ( $null -eq $prefab )
	{
		continue
	}

	$match = Find-NodeWithParent $prefab.RootObject $contract.Guid
	if ( $null -eq $match )
	{
		$failures.Add( "$($contract.Weapon) is missing node $($contract.Guid)." )
		continue
	}

	$node = $match.Node
	$parent = $match.Parent
	if ( $node.Name -ne $contract.Name )
	{
		$failures.Add( "$($contract.Weapon) node $($contract.Guid) is '$($node.Name)' instead of '$($contract.Name)'." )
	}
	if ( $null -eq $parent -or $parent.Name -ne $contract.Parent -or $parent.__guid -ne $contract.ParentGuid )
	{
		$actualParent = if ( $null -eq $parent ) { '<none>' } else { "$($parent.Name) ($($parent.__guid))" }
		$failures.Add( "$($contract.Weapon) $($contract.Name) is parented to '$actualParent' instead of '$($contract.Parent) ($($contract.ParentGuid))'." )
	}
	foreach ( $field in @('Position', 'Rotation', 'Scale') )
	{
		if ( $node.$field -ne $contract.$field )
		{
			$failures.Add( "$($contract.Weapon) $($contract.Name) $field changed from '$($contract.$field)' to '$($node.$field)'." )
		}
	}

	if ( $contract.Contains( 'Model' ) )
	{
		$components = @($node.Components)
		$renderers = @($components | Where-Object {
			(Get-ObjectProperty $_ '__type') -eq 'Sandbox.ModelRenderer' -and
			(Get-ObjectProperty $_ '__guid') -eq $contract.ComponentGuid -and
			(Get-ObjectProperty $_ 'Model') -eq $contract.Model
		})
		if ( $components.Count -ne 1 -or $renderers.Count -ne 1 )
		{
			$failures.Add( "$($contract.Weapon) $($contract.Name) must own exactly renderer $($contract.ComponentGuid) for '$($contract.Model)'." )
		}
		$globalRenderers = @(Find-ComponentsByType $prefab.RootObject 'Sandbox.ModelRenderer' | Where-Object {
			(Get-ObjectProperty $_ 'Model') -eq $contract.Model
		})
		if ( $globalRenderers.Count -ne 1 )
		{
			$failures.Add( "$($contract.Weapon) prefab contains $($globalRenderers.Count) renderers for '$($contract.Model)' instead of exactly one." )
		}
		if ( @($node.Children).Count -ne 0 )
		{
			$failures.Add( "$($contract.Weapon) $($contract.Name) must remain a leaf renderer node." )
		}
	}
	else
	{
		$children = @($node.Children)
		if ( @($node.Components).Count -ne 0 -or $children.Count -ne 1 -or $children[0].__guid -ne $contract.ExpectedChildGuid )
		{
			$failures.Add( "$($contract.Weapon) $($contract.Name) must remain an empty bind wrapper with child $($contract.ExpectedChildGuid)." )
		}
	}

	$rows.Add( [ordered]@{
		weapon = $contract.Weapon
		node = $contract.Name
		parent = if ( $null -eq $parent ) { $null } else { $parent.Name }
		transformPreserved = $node.Position -eq $contract.Position -and
			$node.Rotation -eq $contract.Rotation -and $node.Scale -eq $contract.Scale
	} )
}

$rendererContracts = @(
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'; Name = 'weapon_root_children'; Parent = 'weapon_root'; RendererGuid = '24afea19-e539-58b9-87d0-d16e5cbfbde6'; RendererName = 'ar15_body_renderer_wrapper'; Position = '-1.02060864,0.39162116,4.2278001'; Rotation = '0.0000021,0.02097885,-0.00000595,0.99977992'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_body.vmdl' }
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = 'a3a0906e-e878-4680-ae02-df7f47aaa61b'; Name = 'stock'; Parent = 'weapon_root_children'; RendererGuid = '2e00397f-d895-5ca7-8ca8-ad688bfc468a'; RendererName = 'ar15_stock_renderer_wrapper'; Position = '-4.2214748,-0.17326325,3.40848362'; Rotation = '-0.02523236,-0.02608563,-0.99934107,0.00054147'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_stock.vmdl' }
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = '7614ea12-5a0a-4963-9657-31e2cae17551'; Name = 'trigger'; Parent = 'weapon_root_children'; RendererGuid = '2dc5375e-78e7-565b-92a5-bfeba2dcdf3e'; RendererName = 'ar15_trigger_renderer_wrapper'; Position = '-4.87862575,0.1732638,-1.17874063'; Rotation = '0.01806276,-0.72448217,0.0188275,0.68879956'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_trigger.vmdl' }
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = 'ad4cb462-dbe1-4efb-8bc1-4261fe6ab0d5'; Name = 'magazine'; Parent = 'weapon_root_children'; RendererGuid = '0284e57d-bc10-5878-886e-09bfeeafef57'; RendererName = 'ar15_magazine_renderer_wrapper'; Position = '-6.49246547,0.17325305,-4.08436889'; Rotation = '0.01806273,-0.72448165,0.01882849,0.68880009'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_magazine.vmdl' }
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = 'a87758af-d8f1-4868-9087-bf9c6caf5039'; Name = 'bolt_flap'; Parent = 'weapon_root_children'; RendererGuid = '4fd051fe-190a-5dc7-9346-839f367f34a9'; RendererName = 'ar15_bolt_flap_renderer_wrapper'; Position = '-3.61620847,3.75844976,0.27409323'; Rotation = '0.54535207,-0.56863568,-0.44877128,0.42172137'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_bolt_flap.vmdl' }
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = '184190f3-aa91-4c60-aac3-ce223e688168'; Name = 'bolt'; Parent = 'weapon_root_children'; RendererGuid = 'f34fdcde-ad2e-5401-8e38-8d967ddba4a2'; RendererName = 'ar15_bolt_renderer_wrapper'; Position = '-2.92187056,0.17325287,3.4084745'; Rotation = '0.02608663,-0.02523136,0.00054149,0.99934107'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_bolt.vmdl' }
	[ordered]@{ Weapon = 'AR-15'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'; Guid = '796a5d2c-44a8-4c26-97d7-1d39725df8f3'; Name = 'charging_handle'; Parent = 'weapon_root_children'; RendererGuid = '139ae980-b3dd-5133-adc3-cffdf145788a'; RendererName = 'ar15_charging_handle_renderer_wrapper'; Position = '1.46678811,0.17326138,2.43734737'; Rotation = '0.02608563,-0.02523136,0.00054147,0.9993411'; Scale = '0.79882523,0.79882523,0.79882523'; Model = 'addons/lifepunch/lpweapons/ar15/models/native_candidate/ar15_charging_handle.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-00000000004c'; Name = 'weapon_root_children'; Parent = 'weapon_root'; RendererGuid = '2e5d46a4-5094-50bd-a47f-5818e7c0c14f'; RendererName = 'sr25_body_renderer_wrapper'; Position = '5.83231808,0.12241314,6.50982284'; Rotation = '0,0,0,1'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_body.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000032'; Name = 'stock'; Parent = 'weapon_root_children'; RendererGuid = 'aa634ae8-0907-5cd5-a3da-3a077cdcba7e'; RendererName = 'sr25_stock_renderer_wrapper'; Position = '-10.8352807,0.23099896,6.29584383'; Rotation = '-0.04619199,-0.026089,-0.99859184,0'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_stock.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000027'; Name = 'trigger'; Parent = 'weapon_root_children'; RendererGuid = '5ef893e7-78cc-5566-8a66-83c8b4625349'; RendererName = 'sr25_trigger_renderer_wrapper'; Position = '-7.76597344,-0.23100613,5.43507026'; Rotation = '0.018448,-0.7387731,0.018447,0.67344909'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_trigger.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000038'; Name = 'magazine'; Parent = 'weapon_root_children'; RendererGuid = 'b40efac0-8ec4-549b-9bbf-5f41e4fdbacf'; RendererName = 'sr25_magazine_renderer_wrapper'; Position = '-9.3798031,-0.23101064,2.52944678'; Rotation = '0.01844799,-0.73877259,0.01844799,0.67344963'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_magazine.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-00000000002f'; Name = 'mode_selector'; Parent = 'weapon_root_children'; RendererGuid = '196b56d7-f191-518a-affb-55dc55bb936f'; RendererName = 'sr25_mode_selector_renderer_wrapper'; Position = '-6.50818026,0.79396528,7.57014349'; Rotation = '-0.04619199,-0.026089,-0.99859184,0'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_mode_selector.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000036'; Name = 'bolt_flap'; Parent = 'weapon_root_children'; RendererGuid = '7aeb6592-09a1-5efd-9d16-098e25c98d48'; RendererName = 'sr25_bolt_flap_renderer_wrapper'; Position = '-6.37336033,-2.85535923,-0.67371984'; Rotation = '0.53581307,-0.57736007,-0.46011206,0.40970305'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_bolt_flap.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000005'; Name = 'bolt'; Parent = 'weapon_root_children'; RendererGuid = 'ed371a99-4450-5cec-a969-65373e634488'; RendererName = 'sr25_bolt_renderer_wrapper'; Position = '3.69194117,-0.23101415,6.29582069'; Rotation = '0.02609,-0.04619099,0,0.99859186'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_bolt.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000028'; Name = 'charging_handle'; Parent = 'weapon_root_children'; RendererGuid = '01b85f54-5a3f-563e-8859-9a3fd45a2a7d'; RendererName = 'sr25_charging_handle_renderer_wrapper'; Position = '8.0805998,-0.23100048,5.32469438'; Rotation = '0.026089,-0.04619099,0,0.99859189'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_charging_handle.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-00000000005f'; Name = 'sr25_scope'; Parent = 'weapon_root_children'; RendererGuid = '404b0c18-76aa-5a47-bd89-c9c7e02ac557'; RendererName = 'sr25_scope_renderer_wrapper'; Position = '5.83231808,0.12241314,6.50982284'; Rotation = '0,0,0,1'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_scope.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000061'; Name = 'sr25_scope_mount'; Parent = 'weapon_root_children'; RendererGuid = '10c7e161-2773-54e8-8b29-582c1b0e5f4a'; RendererName = 'sr25_scope_mount_renderer_wrapper'; Position = '5.83231808,0.12241314,6.50982284'; Rotation = '0,0,0,1'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_scope_mount.vmdl' }
	[ordered]@{ Weapon = 'SR-25'; Prefab = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'; Guid = '57250000-5a25-4000-8000-000000000063'; Name = 'sr25_suppressor'; Parent = 'weapon_root_children'; RendererGuid = '5a1fdc84-9fd2-5ad8-9681-bd075423a440'; RendererName = 'sr25_suppressor_renderer_wrapper'; Position = '5.83231808,0.12241314,6.50982284'; Rotation = '0,0,0,1'; Scale = '0.87273486,0.87273486,0.87273486'; Model = 'addons/lifepunch/lpweapons/sr25/models/native_candidate/sr25_suppressor.vmdl' }
)

$rendererOwnerParentGuids = @{
	'df50623e-7a59-4bed-a6e8-65a9483a4dd9' = 'a436e623-2f6b-4cff-8639-1ad59beec5d5'
	'a3a0906e-e878-4680-ae02-df7f47aaa61b' = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'
	'7614ea12-5a0a-4963-9657-31e2cae17551' = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'
	'ad4cb462-dbe1-4efb-8bc1-4261fe6ab0d5' = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'
	'a87758af-d8f1-4868-9087-bf9c6caf5039' = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'
	'184190f3-aa91-4c60-aac3-ce223e688168' = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'
	'796a5d2c-44a8-4c26-97d7-1d39725df8f3' = 'df50623e-7a59-4bed-a6e8-65a9483a4dd9'
	'57250000-5a25-4000-8000-00000000004c' = '57250000-5a25-4000-8000-000000000034'
	'57250000-5a25-4000-8000-000000000032' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000027' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000038' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-00000000002f' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000036' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000005' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000028' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-00000000005f' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000061' = '57250000-5a25-4000-8000-00000000004c'
	'57250000-5a25-4000-8000-000000000063' = '57250000-5a25-4000-8000-00000000004c'
}

$rendererComponentGuids = @{
	'24afea19-e539-58b9-87d0-d16e5cbfbde6' = '45f5ca26-f233-4c12-929b-3e44444f60bc'
	'2e00397f-d895-5ca7-8ca8-ad688bfc468a' = '68ff4e70-374a-484e-afa3-27bce99f6252'
	'2dc5375e-78e7-565b-92a5-bfeba2dcdf3e' = 'b5a13ce8-484f-4e74-bcf2-3943e01a9808'
	'0284e57d-bc10-5878-886e-09bfeeafef57' = 'cdc94a87-0979-4f5c-bb38-50757cdf24fd'
	'4fd051fe-190a-5dc7-9346-839f367f34a9' = '91c5a0d0-c18e-471f-a138-a022b0b2cc78'
	'f34fdcde-ad2e-5401-8e38-8d967ddba4a2' = 'f888005b-4ec8-4922-bf0f-d2ed9ccc3adb'
	'139ae980-b3dd-5133-adc3-cffdf145788a' = 'dbee7b0b-308d-4bd9-8d15-d959f15fca9b'
	'2e5d46a4-5094-50bd-a47f-5818e7c0c14f' = '57250000-5a25-4000-8000-000000000014'
	'aa634ae8-0907-5cd5-a3da-3a077cdcba7e' = '57250000-5a25-4000-8000-000000000022'
	'5ef893e7-78cc-5566-8a66-83c8b4625349' = '57250000-5a25-4000-8000-00000000003b'
	'b40efac0-8ec4-549b-9bbf-5f41e4fdbacf' = '57250000-5a25-4000-8000-000000000043'
	'196b56d7-f191-518a-affb-55dc55bb936f' = '57250000-5a25-4000-8000-00000000005e'
	'7aeb6592-09a1-5efd-9d16-098e25c98d48' = '57250000-5a25-4000-8000-00000000002b'
	'ed371a99-4450-5cec-a969-65373e634488' = '57250000-5a25-4000-8000-000000000056'
	'01b85f54-5a3f-563e-8859-9a3fd45a2a7d' = '57250000-5a25-4000-8000-00000000004a'
	'404b0c18-76aa-5a47-bd89-c9c7e02ac557' = '57250000-5a25-4000-8000-000000000060'
	'10c7e161-2773-54e8-8b29-582c1b0e5f4a' = '57250000-5a25-4000-8000-000000000062'
	'5a1fdc84-9fd2-5ad8-9681-bd075423a440' = '57250000-5a25-4000-8000-000000000064'
}

foreach ( $contract in $rendererContracts )
{
	if ( !$prefabs.ContainsKey( $contract.Prefab ) )
	{
		$prefabs[$contract.Prefab] = Read-Prefab $contract.Prefab
	}
	$prefab = $prefabs[$contract.Prefab]
	if ( $null -eq $prefab )
	{
		continue
	}

	$match = Find-NodeWithParent $prefab.RootObject $contract.Guid
	if ( $null -eq $match )
	{
		$failures.Add( "$($contract.Weapon) is missing donor node $($contract.Guid)." )
		continue
	}

	$node = $match.Node
	$parent = $match.Parent
	$actualParent = if ( $null -eq $parent ) { '<none>' } else { $parent.Name }
	if ( $node.Name -ne $contract.Name )
	{
		$failures.Add( "$($contract.Weapon) donor node $($contract.Guid) is '$($node.Name)' instead of '$($contract.Name)'." )
	}
	$expectedParentGuid = $rendererOwnerParentGuids[$contract.Guid]
	if ( $actualParent -ne $contract.Parent -or $null -eq $parent -or $parent.__guid -ne $expectedParentGuid )
	{
		$actualParentWithGuid = if ( $null -eq $parent ) { '<none>' } else { "$actualParent ($($parent.__guid))" }
		$failures.Add( "$($contract.Weapon) donor node $($contract.Name) is parented to '$actualParentWithGuid' instead of '$($contract.Parent) ($expectedParentGuid)'." )
	}

	$rendererMatch = Find-NodeWithParent $prefab.RootObject $contract.RendererGuid
	if ( $null -eq $rendererMatch )
	{
		$failures.Add( "$($contract.Weapon) is missing renderer wrapper $($contract.RendererGuid)." )
		continue
	}

	$rendererNode = $rendererMatch.Node
	$rendererParent = $rendererMatch.Parent
	if ( $rendererNode.Name -ne $contract.RendererName )
	{
		$failures.Add( "$($contract.Weapon) renderer wrapper $($contract.RendererGuid) is '$($rendererNode.Name)' instead of '$($contract.RendererName)'." )
	}
	if ( $null -eq $rendererParent -or $rendererParent.__guid -ne $contract.Guid )
	{
		$actualRendererParent = if ( $null -eq $rendererParent ) { '<none>' } else { "$($rendererParent.Name) ($($rendererParent.__guid))" }
		$failures.Add( "$($contract.Weapon) renderer wrapper $($contract.RendererName) is parented to $actualRendererParent instead of $($contract.Name) ($($contract.Guid))." )
	}

	$expectedComponentGuid = $rendererComponentGuids[$contract.RendererGuid]
	$modelRenderers = @($rendererNode.Components | Where-Object {
		(Get-ObjectProperty $_ '__type') -eq 'Sandbox.ModelRenderer' -and
		(Get-ObjectProperty $_ '__guid') -eq $expectedComponentGuid -and
		(Get-ObjectProperty $_ 'Model') -eq $contract.Model
	})
	if ( @($rendererNode.Components).Count -ne 1 -or $modelRenderers.Count -ne 1 -or @($rendererNode.Children).Count -ne 0 )
	{
		$failures.Add( "$($contract.Weapon) renderer wrapper $($contract.RendererName) must be a leaf with exactly renderer $expectedComponentGuid for '$($contract.Model)'." )
	}
	$ownerRenderers = @($node.Components | Where-Object {
		(Get-ObjectProperty $_ '__type') -eq 'Sandbox.ModelRenderer'
	})
	if ( $ownerRenderers.Count -ne 0 )
	{
		$failures.Add( "$($contract.Weapon) donor owner $($contract.Name) retained a direct ModelRenderer outside its wrapper." )
	}
	$globalRenderers = @(Find-ComponentsByType $prefab.RootObject 'Sandbox.ModelRenderer' | Where-Object {
		(Get-ObjectProperty $_ 'Model') -eq $contract.Model
	})
	if ( $globalRenderers.Count -ne 1 )
	{
		$failures.Add( "$($contract.Weapon) prefab contains $($globalRenderers.Count) renderers for '$($contract.Model)' instead of exactly one." )
	}
	$ownerTransformPreserved = $node.Position -eq '0,0,0' -and $node.Rotation -eq '0,0,0,1' -and $node.Scale -eq '1,1,1'
	$rendererTransformPreserved = $rendererNode.Position -eq $contract.Position -and
		$rendererNode.Rotation -eq $contract.Rotation -and $rendererNode.Scale -eq $contract.Scale
	$transformPreserved = $ownerTransformPreserved -and $rendererTransformPreserved
	if ( !$transformPreserved )
	{
		$failures.Add( "$($contract.Weapon) $($contract.Name)/$($contract.RendererName) transform contract changed." )
	}

	$rows.Add( [ordered]@{
		weapon = $contract.Weapon
		node = $contract.RendererName
		parent = $contract.Name
		model = $contract.Model
		mappingKind = 'donor-renderer-wrapper'
		transformPreserved = $transformPreserved
	} )
}

$sr25AnchorContracts = @(
	[ordered]@{ Name = 'sr25_muzzle'; Guid = '57250000-5a25-4000-8000-000000000065'; ParentName = 'weapon_root_children'; ParentGuid = '57250000-5a25-4000-8000-00000000004c'; Position = '24.97019353,0.0386306,7.29964789'; Rotation = '0,0,0,1'; Scale = '1,1,1' }
	[ordered]@{ Name = 'sr25_ejection_port'; Guid = '57250000-5a25-4000-8000-000000000066'; ParentName = 'bolt_flap'; ParentGuid = '57250000-5a25-4000-8000-000000000036'; Position = '-5.9303435,2.28924229,-1.4332413'; Rotation = '0.53581307,-0.57736007,-0.46011206,0.40970305'; Scale = '1,1,1' }
)

$sr25Prefab = $prefabs['game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab']
if ( $null -ne $sr25Prefab )
{
	foreach ( $contract in $sr25AnchorContracts )
	{
		$match = Find-NodeWithParent $sr25Prefab.RootObject $contract.Guid
		if ( $null -eq $match )
		{
			$failures.Add( "SR-25 is missing functional anchor $($contract.Name) ($($contract.Guid))." )
			continue
		}

		$node = $match.Node
		$parent = $match.Parent
		$parentValid = $null -ne $parent -and $parent.Name -eq $contract.ParentName -and $parent.__guid -eq $contract.ParentGuid
		$transformValid = $node.Name -eq $contract.Name -and
			$node.Position -eq $contract.Position -and
			$node.Rotation -eq $contract.Rotation -and
			$node.Scale -eq $contract.Scale
		if ( !$parentValid -or !$transformValid )
		{
			$failures.Add( "SR-25 functional anchor $($contract.Name) changed parent or transform contract." )
		}

		$rows.Add( [ordered]@{
			weapon = 'SR-25'
			node = $contract.Name
			parent = $contract.ParentName
			mappingKind = 'functional-anchor'
			transformPreserved = $parentValid -and $transformValid
		} )
	}
}

$patchContracts = @(
	[ordered]@{ Weapon = 'M1911'; Guid = 'f3a5a630-0d36-55ef-89ae-30db6d72c007'; Name = 'm1911_body'; ParentGuid = '2f9a4399-5075-4c53-a4f7-6722110fa2e2'; ParentInstanceGuid = 'be655d9a-d1a6-5a37-86ef-693690409455'; Parent = 'weapon_root_children'; ComponentGuid = 'a2c32a8c-21d4-5dd9-80f9-0806834b5faf'; Position = '-0.88245009999999979,0.074942410000000001,3.4594364'; Rotation = '0,0,0,1'; Scale = '1,1,1'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_body.vmdl' }
	[ordered]@{ Weapon = 'M1911'; Guid = '6a1eb4d5-fd63-5899-b973-3b628482aca4'; Name = 'm1911_slide'; ParentGuid = '38b6ec59-6293-4ece-901a-0868678dcd2c'; ParentInstanceGuid = '3df5bcb7-967a-54fd-8d9a-31dce27f441a'; Parent = 'slide'; ComponentGuid = 'd330855c-40eb-5f97-b652-c1f0abbb5547'; Position = '-2.2053200999999998,0.074942410000000001,1.8289033999999997'; Rotation = '0,0,0,1'; Scale = '1,1,1'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_slide.vmdl' }
	[ordered]@{ Weapon = 'M1911'; Guid = 'ad1196a1-5216-5d92-a93a-be54233bec30'; Name = 'm1911_magazine'; ParentGuid = '56d6853a-544c-46a5-b70a-380bd2a745c8'; ParentInstanceGuid = '6204a3dd-86f0-5750-8069-8feccc8fe446'; Parent = 'magazine'; ComponentGuid = '51ca9973-8792-529d-b1e7-21b9f7b50fa5'; Position = '-3.8946310928797931,0.074942410000000001,-1.9602429314329672'; Rotation = '0,-0.79863533644524842,0,0.60181525352967336'; Scale = '1.0000000000001104,1,1.0000000000001104'; Model = 'addons/lifepunch/lpweapons/m1911/models/m1911_magazine.vmdl' }
)
$m1911PrefabPath = if ( [string]::IsNullOrWhiteSpace( $M1911PrefabOverride ) )
{
	'game\Assets\addons\lifepunch\lpweapons\m1911\equipment\vm_m1911\vm_m1911.prefab'
}
else
{
	$M1911PrefabOverride
}
$prefabs[$m1911PrefabPath] = Read-Prefab $m1911PrefabPath
$m1911Prefab = $prefabs[$m1911PrefabPath]
if ( $null -ne $m1911Prefab )
{
	$instancePatchProperty = $m1911Prefab.RootObject.PSObject.Properties['__PrefabInstancePatch']
	if ( $null -ne $instancePatchProperty )
	{
		$addedObjects = @($instancePatchProperty.Value.AddedObjects)
		foreach ( $contract in $patchContracts )
		{
			$matches = @($addedObjects | Where-Object { $_.Id.IdValue -eq $contract.Guid })
			if ( $matches.Count -ne 1 )
			{
				$failures.Add( "M1911 patch has $($matches.Count) added objects for $($contract.Guid) instead of exactly one." )
				continue
			}

			$added = $matches[0]
			$node = $added.Data
			if ( $node.Name -ne $contract.Name )
			{
				$failures.Add( "M1911 patch node $($contract.Guid) is '$($node.Name)' instead of '$($contract.Name)'." )
			}
			if ( $added.Parent.IdValue -ne $contract.ParentGuid )
			{
				$failures.Add( "M1911 $($contract.Name) parent changed from $($contract.ParentGuid) to $($added.Parent.IdValue)." )
			}
			$modelRenderers = @($node.Components | Where-Object {
				(Get-ObjectProperty $_ '__type') -eq 'Sandbox.ModelRenderer' -and
				(Get-ObjectProperty $_ '__guid') -eq $contract.ComponentGuid -and
				(Get-ObjectProperty $_ 'Model') -eq $contract.Model
			})
			if ( @($node.Components).Count -ne 1 -or $modelRenderers.Count -ne 1 -or @($node.Children).Count -ne 0 )
			{
				$failures.Add( "M1911 $($contract.Name) has $($modelRenderers.Count) renderers for '$($contract.Model)' instead of exactly one." )
			}
			$transformPreserved = $node.Position -eq $contract.Position -and
				$node.Rotation -eq $contract.Rotation -and $node.Scale -eq $contract.Scale
			if ( !$transformPreserved )
			{
				$failures.Add( "M1911 $($contract.Name) changed from its pinned bind-local transform." )
			}

			$rows.Add( [ordered]@{
				weapon = $contract.Weapon
				node = $contract.Name
				parent = $contract.Parent
				model = $contract.Model
				mappingKind = 'inherited-prefab-patch'
				transformPreserved = $transformPreserved
			} )
		}
	}
	else
	{
		$viewModels = @($m1911Prefab.RootObject.Components | Where-Object {
			(Get-ObjectProperty $_ '__type') -eq 'Dxura.RP.Game.ViewModel' -and
			(Get-ObjectProperty $_ '__guid') -eq 'becae687-6ef6-57de-a240-5bccff1b9a0f'
		})
		if ( $viewModels.Count -ne 1 )
		{
			$failures.Add( "Materialized M1911 root has $($viewModels.Count) ViewModel components instead of exactly one." )
		}
		$uspDrivers = @($m1911Prefab.RootObject.Components | Where-Object {
			(Get-ObjectProperty $_ '__type') -eq 'Sandbox.SkinnedModelRenderer' -and
			(Get-ObjectProperty $_ '__guid') -eq '26f8f728-ca2d-5360-b500-a04a0b455ff4' -and
			(Get-ObjectProperty $_ 'Model') -eq 'models/weapons/sbox_pistol_usp/v_usp.vmdl' -and
			(Get-ObjectProperty $_ 'MaterialOverride') -eq 'addons/lifepunch/lpweapons/m1911/equipment/vm_m1911/invisible.vmat'
		})
		if ( $uspDrivers.Count -ne 1 )
		{
			$failures.Add( "Materialized M1911 root has $($uspDrivers.Count) hidden USP driver renderers instead of exactly one." )
		}
		if ( $viewModels.Count -eq 1 -and $viewModels[0].AdditionalRendererRoot.go -ne '994ee179-fd62-56a8-9c0a-077d01b34ec9' )
		{
			$failures.Add( 'Materialized M1911 ViewModel AdditionalRendererRoot must remain pinned to weapon_root.' )
		}

		foreach ( $contract in $patchContracts )
		{
			$match = Find-NodeWithParent $m1911Prefab.RootObject $contract.Guid
			if ( $null -eq $match )
			{
				$failures.Add( "Materialized M1911 is missing $($contract.Name) ($($contract.Guid))." )
				continue
			}

			$node = $match.Node
			$parent = $match.Parent
			if ( $node.Name -ne $contract.Name )
			{
				$failures.Add( "Materialized M1911 node $($contract.Guid) is '$($node.Name)' instead of '$($contract.Name)'." )
			}
			if ( $null -eq $parent -or $parent.__guid -ne $contract.ParentInstanceGuid -or $parent.Name -ne $contract.Parent )
			{
				$actualParent = if ( $null -eq $parent ) { '<none>' } else { "$($parent.Name) ($($parent.__guid))" }
				$failures.Add( "Materialized M1911 $($contract.Name) parent is $actualParent instead of $($contract.Parent) ($($contract.ParentInstanceGuid))." )
			}
			$modelRenderers = @($node.Components | Where-Object {
				(Get-ObjectProperty $_ '__type') -eq 'Sandbox.ModelRenderer' -and
				(Get-ObjectProperty $_ '__guid') -eq $contract.ComponentGuid -and
				(Get-ObjectProperty $_ 'Model') -eq $contract.Model
			})
			if ( @($node.Components).Count -ne 1 -or $modelRenderers.Count -ne 1 -or @($node.Children).Count -ne 0 )
			{
				$failures.Add( "Materialized M1911 $($contract.Name) has $($modelRenderers.Count) renderers for '$($contract.Model)' instead of exactly one." )
			}
			$transformPreserved = $node.Position -eq $contract.Position -and
				$node.Rotation -eq $contract.Rotation -and $node.Scale -eq $contract.Scale
			if ( !$transformPreserved )
			{
				$failures.Add( "Materialized M1911 $($contract.Name) changed from its pinned bind-local transform." )
			}

			$rows.Add( [ordered]@{
				weapon = $contract.Weapon
				node = $contract.Name
				parent = if ( $null -eq $parent ) { $null } else { $parent.Name }
				model = $contract.Model
				mappingKind = 'materialized-donor-hierarchy'
				transformPreserved = $transformPreserved
			} )
		}
	}
}

$sr25PrefabPath = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'
$sr25Prefab = $prefabs[$sr25PrefabPath]
$scopeRootValid = $false
if ( $null -ne $sr25Prefab )
{
	$viewModels = @($sr25Prefab.RootObject.Components | Where-Object {
		(Get-ObjectProperty $_ '__type') -eq 'Dxura.RP.Game.ViewModel'
	})
	if ( $viewModels.Count -ne 1 )
	{
		$failures.Add( "SR-25 view prefab has $($viewModels.Count) ViewModel components instead of exactly one." )
	}
	else
	{
		$viewModel = $viewModels[0]
		$scopeRoot = Find-NodeWithParent $sr25Prefab.RootObject $viewModel.AdditionalRendererRoot.go
		$scopeRootValid = $viewModel.CanADS -eq $false -and
			$viewModel.AdditionalRendererRoot.go -eq '57250000-5a25-4000-8000-000000000034' -and
			$null -ne $scopeRoot -and $scopeRoot.Node.Name -eq 'weapon_root'
		if ( !$scopeRootValid )
		{
			$failures.Add( 'SR-25 must disable ordinary ADS and hide the complete custom weapon_root through AdditionalRendererRoot while scoped.' )
		}
	}
}

$sr25WorldPath = 'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\w_sr25\w_sr25.prefab'
$sr25World = Read-Prefab $sr25WorldPath
$prefabs[$sr25WorldPath] = $sr25World
$semiOnly = $false
if ( $null -ne $sr25World )
{
	$shootComponents = @(Find-ComponentsByType $sr25World.RootObject 'Dxura.RP.Game.ShootWeaponComponent')
	if ( $shootComponents.Count -ne 1 )
	{
		$failures.Add( "SR-25 world prefab has $($shootComponents.Count) ShootWeaponComponent instances instead of exactly one." )
	}
	else
	{
		$shoot = $shootComponents[0]
		$supported = @($shoot.SupportedFireModes)
		$semiOnly = $shoot.CurrentFireMode -eq 'Semi' -and $supported.Count -eq 1 -and $supported[0] -eq 'Semi'
		if ( !$semiOnly )
		{
			$failures.Add( 'SR-25 must remain semi-only and must not inherit a bolt-action or automatic firing contract.' )
		}
	}
}

$candidatePrefabs = @(
	'game\Assets\addons\lifepunch\lpweapons\aks74u\equipment\vm_aks74u\vm_aks74u.prefab'
	'game\Assets\addons\lifepunch\lpweapons\ar15\equipment\vm_ar15\vm_ar15.prefab'
	'game\Assets\addons\lifepunch\lpweapons\deserteagle\equipment\vm_desert_eagle\vm_desert_eagle.prefab'
	'game\Assets\addons\lifepunch\lpweapons\sr25\equipment\vm_sr25\vm_sr25.prefab'
	'game\Assets\addons\lifepunch\lpweapons\m1911\equipment\vm_m1911\vm_m1911.prefab'
	'game\Assets\addons\lifepunch\lpweapons\m870\equipment\vm_m870\vm_m870.prefab'
)
$boltPusherConsumers = 0
foreach ( $candidatePrefab in $candidatePrefabs )
{
	$path = Join-Path $repoRoot $candidatePrefab
	if ( Test-Path -LiteralPath $path -PathType Leaf )
	{
		$text = Get-Content -LiteralPath $path -Raw
		$boltPusherConsumers += ([regex]::Matches( $text, 'Dxura\.RP\.Game\.BoltPusher' )).Count
	}
}
if ( $boltPusherConsumers -ne 0 )
{
	$failures.Add( "Custom candidates must not acquire BoltPusher as a fake moving-part or audio driver; found $boltPusherConsumers." )
}

$summary = [ordered]@{
	result = if ( $failures.Count -eq 0 ) { 'PASS' } else { 'FAIL' }
	prefabs = $prefabs.Count
	mappings = $contracts.Count + $rendererContracts.Count + $sr25AnchorContracts.Count + $patchContracts.Count
	verifiedMappings = $rows.Count
	boltPusherConsumers = $boltPusherConsumers
	scopeRootValid = $scopeRootValid
	semiOnly = $semiOnly
	rows = @($rows)
	failures = @($failures)
}

$summary | ConvertTo-Json -Depth 6 -Compress
if ( $failures.Count -gt 0 )
{
	exit 1
}

$preservedTransforms = @($rows | Where-Object { $_.transformPreserved }).Count
Write-Output "RESULT weapon_animation_hierarchy=PASS prefabs=$($prefabs.Count) mappings=$($rows.Count) transforms_preserved=$preservedTransforms bolt_pusher_consumers=$boltPusherConsumers scope_root_valid=$([int]$scopeRootValid) semi_only=$([int]$semiOnly)"
