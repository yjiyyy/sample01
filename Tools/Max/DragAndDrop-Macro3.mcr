-- 바이패드 FBX 머지 (본체 포함). UTF-8 BOM.
-- 업데이트 시: Tools\Max\BipedFbxMergeTool.ms 내용을 이 파일 macroScript ( ) 안에 다시 넣으세요.
macroScript Macro3
	category:"DragAndDrop"
	toolTip:"바이패드 FBX 머지"
	buttonText:"FBX머지"
(
	-- 바이패드 FBX 머지 도구 (3ds Max 2023+)
	-- 리타겟/베이크 없음. 유니티 FBX(최상위 Root_Dummy)를 씬 바이패드에 이름 맞춰 머지합니다.
	-- Scripting > Run Script 로 실행.

	try (destroyDialog Sample01_BipedFbxMergeRollout) catch ()

	global Sample01_BipedFbxMerge_BipRoots
	global Sample01_BipedFbxMerge_Title
	if Sample01_BipedFbxMerge_BipRoots == undefined do Sample01_BipedFbxMerge_BipRoots = #()
	if Sample01_BipedFbxMerge_Title == undefined do Sample01_BipedFbxMerge_Title = "바이패드 FBX 머지"

	fn Sample01_BipedFbxMerge_IsBipedNode o =
	(
	    o != undefined and (isKindOf o Biped_Object)
	)

	fn Sample01_BipedFbxMerge_IsBipedCom o =
	(
	    if o == undefined then false
	    else try (classof o.controller == Vertical_Horizontal_Turn) catch (false)
	)

	fn Sample01_BipedFbxMerge_CollectRoots =
	(
	    local roots = #()
	    for o in $* where Sample01_BipedFbxMerge_IsBipedCom o do
	        if (findItem roots o) == 0 do append roots o
	    if roots.count == 0 do
	    (
	        for o in $* where Sample01_BipedFbxMerge_IsBipedNode o do
	        (
	            local r = try (biped.getRootNode o) catch (undefined)
	            if r == undefined and (o.parent == undefined or not (Sample01_BipedFbxMerge_IsBipedNode o.parent)) do r = o
	            if r != undefined and (findItem roots r) == 0 do append roots r
	        )
	    )
	    roots
	)

	fn Sample01_BipedFbxMerge_BelongsToRoot o bipRoot =
	(
	    if o == undefined or bipRoot == undefined then false
	    else if o == bipRoot then true
	    else
	    (
	        local r = try (biped.getRootNode o) catch (undefined)
	        if r == bipRoot then true
	        else
	        (
	            local p = o
	            local found = false
	            while p != undefined and found == false do
	            (
	                if p == bipRoot do found = true
	                p = p.parent
	            )
	            found
	        )
	    )
	)

	fn Sample01_BipedFbxMerge_IsFingerOrToe n =
	(
	    (matchPattern n pattern:"*Finger*" ignoreCase:true) or (matchPattern n pattern:"*Toe*" ignoreCase:true)
	)

	fn Sample01_BipedFbxMerge_ClearNodeAnim o =
	(
	    try (deleteKeys o.transform.controller #allKeys) catch ()
	    try (deleteKeys o.position.controller #allKeys) catch ()
	    try (deleteKeys o.rotation.controller #allKeys) catch ()
	    try (deleteKeys o.scale.controller #allKeys) catch ()
	)

	fn Sample01_BipedFbxMerge_ResetSceneRootDummy =
	(
	    for o in $* where (stricmp o.name "Root_Dummy" == 0) or (stricmp o.name "Root_dummy" == 0) do
	    (
	        Sample01_BipedFbxMerge_ClearNodeAnim o
	        local sc = o.scale
	        o.transform = matrix3 1
	        o.scale = sc
	        o.pos = [0,0,0]
	    )
	)

	fn Sample01_BipedFbxMerge_ClearFingerToeKeys bipRoot =
	(
	    for o in $* where Sample01_BipedFbxMerge_IsBipedNode o do
	    (
	        if (Sample01_BipedFbxMerge_BelongsToRoot o bipRoot) and (Sample01_BipedFbxMerge_IsFingerOrToe o.name) do
	            Sample01_BipedFbxMerge_ClearNodeAnim o
	    )
	)

	fn Sample01_BipedFbxMerge_SceneUnit =
	(
	    case units.SystemType of
	    (
	        #meters: "m"
	        #centimeters: "cm"
	        #millimeters: "mm"
	        #kilometers: "km"
	        #inches: "in"
	        #feet: "ft"
	        #yards: "yd"
	        default: "cm"
	    )
	)

	fn Sample01_BipedFbxMerge_AddCtrlTimes ctrl &minT &maxT &found =
	(
	    if ctrl == undefined do return()
	    try
	    (
	        if ctrl.keys != undefined and ctrl.keys.count > 0 then
	        (
	            local k1 = ctrl.keys[1].time
	            local k2 = ctrl.keys[ctrl.keys.count].time
	            if found == false then (minT = k1; maxT = k2; found = true)
	            else (if k1 < minT do minT = k1; if k2 > maxT do maxT = k2)
	        )
	    )
	    catch ()
	    try (for i = 1 to ctrl.numSubs do Sample01_BipedFbxMerge_AddCtrlTimes ctrl[i].controller &minT &maxT &found) catch ()
	)

	fn Sample01_BipedFbxMerge_GetBipedInterval bipRoot =
	(
	    if bipRoot == undefined then return undefined
	    local minT = 0f
	    local maxT = 0f
	    local found = false
	    try (Sample01_BipedFbxMerge_AddCtrlTimes bipRoot.transform.controller &minT &maxT &found) catch ()
	    try (Sample01_BipedFbxMerge_AddCtrlTimes bipRoot.controller &minT &maxT &found) catch ()
	    for o in $* where Sample01_BipedFbxMerge_IsBipedNode o and (Sample01_BipedFbxMerge_BelongsToRoot o bipRoot) do
	    (
	        try (Sample01_BipedFbxMerge_AddCtrlTimes o.transform.controller &minT &maxT &found) catch ()
	        try (Sample01_BipedFbxMerge_AddCtrlTimes o.position.controller &minT &maxT &found) catch ()
	        try (Sample01_BipedFbxMerge_AddCtrlTimes o.rotation.controller &minT &maxT &found) catch ()
	    )
	    if found then interval minT maxT else undefined
	)

	fn Sample01_BipedFbxMerge_CountComKeys bipRoot =
	(
	    if bipRoot == undefined then return 0
	    local n = 0
	    try
	    (
	        local c = bipRoot.controller
	        if c != undefined and c.keys != undefined do n = c.keys.count
	    )
	    catch ()
	    if n == 0 do
	    (
	        try
	        (
	            local c = bipRoot.transform.controller
	            if c != undefined and c.keys != undefined do n = c.keys.count
	        )
	        catch ()
	    )
	    n
	)

	fn Sample01_BipedFbxMerge_DeleteNewObjects oldObjs =
	(
	    local toDel = #()
	    for o in $* where o != undefined and not (isDeleted o) do
	    (
	        if (findItem oldObjs o) == 0 do
	        (
	            -- 바이패드 본체에 붙은 건 새 오브젝트가 아님. 남은 메시·여분 루트만 지움.
	            if not (Sample01_BipedFbxMerge_IsBipedNode o) do append toDel o
	        )
	    )
	    if toDel.count > 0 do
	    (
	        try (delete toDel) catch ()
	    )
	    toDel.count
	)

	fn Sample01_BipedFbxMerge_AnyFigureMode =
	(
	    Sample01_BipedFbxMerge_BipRoots = Sample01_BipedFbxMerge_CollectRoots()
	    for r in Sample01_BipedFbxMerge_BipRoots do
	    (
	        if (try (biped.figureMode r.controller) catch (false)) do return true
	    )
	    false
	)

	fn Sample01_BipedFbxMerge_DoMerge fbxPath =
	(
	    if fbxPath == undefined or fbxPath == "" or not (doesFileExist fbxPath) then
	    (
	        messageBox "FBX 파일이 없습니다." title:Sample01_BipedFbxMerge_Title
	        return false
	    )

	    Sample01_BipedFbxMerge_BipRoots = Sample01_BipedFbxMerge_CollectRoots()
	    if Sample01_BipedFbxMerge_BipRoots.count == 0 then
	    (
	        messageBox "씬에 바이패드가 없습니다.\n바이패드 .max 를 연 뒤 다시 시도하세요." title:Sample01_BipedFbxMerge_Title
	        return false
	    )

	    if Sample01_BipedFbxMerge_AnyFigureMode() then
	    (
	        messageBox "Figure Mode를 끈 뒤 다시 머지하세요." title:Sample01_BipedFbxMerge_Title
	        return false
	    )

	    local oldObjs = for o in $* collect o
	    local ok = false
	    try
	    (
	        pluginManager.loadClass FBXIMP
	        FBXImporterSetParam "Mode" #merge
	        FBXImporterSetParam "Animation" true
	        FBXImporterSetParam "FillTimeline" true
	        FBXImporterSetParam "KeepFrameRate" true
	        FBXImporterSetParam "BakeAnimationLayers" true
	        FBXImporterSetParam "Skin" false
	        FBXImporterSetParam "Shape" false
	        FBXImporterSetParam "Cameras" false
	        FBXImporterSetParam "Lights" false
	        FBXImporterSetParam "Markers" false
	        FBXImporterSetParam "ScaleConversion" true
	        FBXImporterSetParam "ConvertUnit" (Sample01_BipedFbxMerge_SceneUnit())
	        FBXImporterSetParam "AxisConversion" true
	        FBXImporterSetParam "UpAxis" "Z"
	        FBXImporterSetParam "ImportBoneAsDummy" true
	        ok = importFile fbxPath #noPrompt using:FBXIMP
	    )
	    catch (ok = false)

	    if not ok then
	    (
	        messageBox "FBX를 머지하지 못했습니다." title:Sample01_BipedFbxMerge_Title
	        return false
	    )

	    -- 이름이 맞아 바이패드에 붙은 애니만 남기고, FBX에서 새로 생긴 메시·여분 루트는 제거
	    local deleted = Sample01_BipedFbxMerge_DeleteNewObjects oldObjs
	    Sample01_BipedFbxMerge_ResetSceneRootDummy()

	    Sample01_BipedFbxMerge_BipRoots = Sample01_BipedFbxMerge_CollectRoots()
	    for r in Sample01_BipedFbxMerge_BipRoots do
	        Sample01_BipedFbxMerge_ClearFingerToeKeys r

	    local bipRoot = Sample01_BipedFbxMerge_BipRoots[1]
	    local iv = Sample01_BipedFbxMerge_GetBipedInterval bipRoot
	    if iv != undefined do animationRange = iv

	    local keyCount = Sample01_BipedFbxMerge_CountComKeys bipRoot
	    redrawViews()

	    local msg = "머지 완료\n" + (filenameFromPath fbxPath)
	    if iv != undefined do
	        msg += "\n타임라인 " + (iv.start as string) + " ~ " + (iv.end as string)
	    msg += "\nCOM 키 " + (keyCount as string) + "개"
	    if deleted > 0 do msg += "\n여분 오브젝트 " + (deleted as string) + "개 삭제"
	    msg += "\nRoot_Dummy 원점 유지 / Finger·Toe 키 제거"
	    if keyCount <= 1 do
	        msg += "\n\n키가 거의 없습니다. FBX 최상위가 Root_Dummy 인지, 뼈 이름이 바이패드와 같은지 확인하세요."
	    messageBox msg title:Sample01_BipedFbxMerge_Title
	    true
	)

	rollout Sample01_BipedFbxMergeRollout "바이패드 FBX 머지" width:360 height:280
	(
	    local fbxPath = ""
	    local dropHost = undefined

	    label lblInfo1 "유니티에서 뽑은 FBX를 머지합니다." align:#left
	    label lblInfo2 "최상위 Root_Dummy + 뼈 이름이 Max와 같아야 합니다." align:#left
	    label lblInfo3 "리타겟 없음 · 이름 같은 뼈에 애니가 붙습니다." align:#left

	    edittext edtPath "" width:250 height:20 readOnly:true across:2 align:#left
	    button btnBrowse "찾기..." width:70 height:22 align:#right

	    button btnMerge "FBX 머지" width:330 height:32

	    label lblDrop "아래에 FBX를 끌어다 놓아도 됩니다" align:#left
	    dotNetControl dnDrop "System.Windows.Forms.Label" width:330 height:56

	    label lblStatus "대기 중" align:#left

	    fn setPath p =
	    (
	        if p != undefined and p != "" then
	        (
	            fbxPath = p
	            edtPath.text = p
	            lblStatus.text = "선택: " + (filenameFromPath p)
	        )
	    )

	    fn refreshDropStyle =
	    (
	        try
	        (
	            dnDrop.Text = "여기에 FBX 끌어서 놓기"
	            dnDrop.TextAlign = (dotNetClass "System.Drawing.ContentAlignment").MiddleCenter
	            dnDrop.BorderStyle = (dotNetClass "System.Windows.Forms.BorderStyle").FixedSingle
	            dnDrop.AllowDrop = true
	            dnDrop.BackColor = (dotNetClass "System.Drawing.Color").FromArgb 45 45 48
	            dnDrop.ForeColor = (dotNetClass "System.Drawing.Color").FromArgb 220 220 220
	        )
	        catch ()
	    )

	    on Sample01_BipedFbxMergeRollout open do
	    (
	        Sample01_BipedFbxMerge_BipRoots = Sample01_BipedFbxMerge_CollectRoots()
	        if Sample01_BipedFbxMerge_BipRoots.count == 0 then
	            lblStatus.text = "바이패드 없음 — .max 를 먼저 여세요"
	        else
	            lblStatus.text = "바이패드 " + (Sample01_BipedFbxMerge_BipRoots.count as string) + "개 감지"
	        refreshDropStyle()
	    )

	    on btnBrowse pressed do
	    (
	        local f = getOpenFileName caption:"유니티 FBX 선택" types:"FBX (*.fbx)|*.fbx|All|*.*|"
	        if f != undefined do setPath f
	    )

	    on btnMerge pressed do
	    (
	        if fbxPath == "" then
	        (
	            messageBox "FBX를 고르거나 끌어다 놓으세요." title:Sample01_BipedFbxMerge_Title
	            return()
	        )
	        lblStatus.text = "머지 중..."
	        if Sample01_BipedFbxMerge_DoMerge fbxPath then
	            lblStatus.text = "완료: " + (filenameFromPath fbxPath)
	        else
	            lblStatus.text = "실패 또는 취소"
	    )

	    on dnDrop DragEnter arg do
	    (
	        try
	        (
	            local files = arg.Data.GetData (dotNetClass "System.Windows.Forms.DataFormats").FileDrop
	            if files != undefined and files.Length > 0 then
	            (
	                local n = toLower (files.GetValue 0)
	                if matchPattern n pattern:"*.fbx" then
	                    arg.Effect = (dotNetClass "System.Windows.Forms.DragDropEffects").Copy
	                else
	                    arg.Effect = (dotNetClass "System.Windows.Forms.DragDropEffects").None
	            )
	            else arg.Effect = (dotNetClass "System.Windows.Forms.DragDropEffects").None
	        )
	        catch (arg.Effect = (dotNetClass "System.Windows.Forms.DragDropEffects").None)
	    )

	    on dnDrop DragDrop arg do
	    (
	        try
	        (
	            local files = arg.Data.GetData (dotNetClass "System.Windows.Forms.DataFormats").FileDrop
	            if files != undefined and files.Length > 0 do
	            (
	                local n = files.GetValue 0
	                if matchPattern (toLower n) pattern:"*.fbx" then
	                (
	                    setPath n
	                    lblStatus.text = "머지 중..."
	                    if Sample01_BipedFbxMerge_DoMerge n then
	                        lblStatus.text = "완료: " + (filenameFromPath n)
	                    else
	                        lblStatus.text = "실패 또는 취소"
	                )
	                else messageBox "FBX 파일만 놓을 수 있습니다." title:Sample01_BipedFbxMerge_Title
	            )
	        )
	        catch
	        (
	            messageBox "끌어다 놓기에 실패했습니다. 찾기로 고르세요." title:Sample01_BipedFbxMerge_Title
	        )
	    )
	)

	createDialog Sample01_BipedFbxMergeRollout

)