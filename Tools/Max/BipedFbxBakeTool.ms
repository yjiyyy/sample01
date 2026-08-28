-- 이전 베이크(리타겟) 도구는 머지 방식으로 바뀌었습니다.
-- 이 파일을 실행하면 새 도구가 열립니다.
(
    local dir = getFilenamePath (getSourceFileName())
    fileIn (dir + "BipedFbxMergeTool.ms")
)
