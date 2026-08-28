-- Sample01 바이패드 FBX 머지 버튼 등록용
-- Max: Scripting > Run Script 로 이 파일을 실행한 뒤
-- Customize > Customize User Interface > Toolbars > Category "Sample01" 에서 툴바로 드래그
macroScript Sample01_BipedFbxMerge
	category:"Sample01"
	toolTip:"Biped FBX Merge"
	buttonText:"FBX Merge"
(
	local scriptPath = @"c:\game\Git\sample01\Tools\Max\BipedFbxMergeTool.ms"
	if not (doesFileExist scriptPath) then
		messageBox ("Script not found:\n" + scriptPath) title:"Biped FBX Merge"
	else
		fileIn scriptPath
)
