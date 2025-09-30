# PatchNotesJsonGenerator
 
      [개요]
         Unity Editor에서 한국어 패치 노트를 입력하면 Google Cloud Translation API를 사용해 
         22개 언어로 자동 번역하고 번역된 패치노트를 Json 형식으로 출력 및 자동 생성하는 도구이다.

      [지원국가 및 언어]
         - 한국/한국어
         - 미국/영어
         - 일본/일본어
         - 태국/태국어
         - 중국/중국어(본토), (대만)
         - 인도/힌디어
         - 이탈리아/이탈리아어
         - 프랑스/프랑스어
         - 독일/독일어
         - 인도네시아/인도네시아어
         - 베트남/베트남어
         - 러시아/러시아어
         - 사우디아라비아/아랍어
         - 스웨덴/스웨덴어
         - 스페인/스페인어(유럽)
         - 포르투갈/포르투갈어(브라질)
         - 우크라이나/우크라이나어
         - 터키(튀르키예)/터키어
         - 폴란드/폴란드어
         - 네덜란드/네덜란드어

       [의존성 패키지]
         newtonsoft-json
         - 설치 방법
           Package Manager -> Add package by name -> "com.unity.nuget.newtonsoft-json"

       [설정방법]
         1. Google Cloud Translation API 설정 및 API 키 생성
         2. Unity Editor에서 Tools - Patch Notes Json Generator 실행
         3. Google Cloud Translation API Settings 오픈
         4. API Key 필드에 자신의 API Key 입력
         5. (선택사항) Project ID 입력
    
       [실행 순서]
         1. 언어 선택
             -Language Selection 오픈 후, 번역할 언어 선택
         2. 한국어 패치노트 입력
             - 텍스트 영역에 패치노트 내용 작성
         3. 번역 실행
             - 번역 실행 버튼 클릭 및 진행 상황 확인
         4. 결과 확인 및 수정
             - 번역 결과 미리보기에서 각 언어별로 번역된 내용 확인 및 필요시 수정
         5. 클립보드 복사 및 JSON 형식으로 파일 저장
             - Unity Editor - Asset - UpdateLogs 폴더에 버전(218 -> 2.1.8) 이름으로 파일 생성


       [실행화면]
    <img width="1307" height="1043" alt="image" src="https://github.com/user-attachments/assets/ba81b410-2f61-4e93-a6be-4e54963d7a14" /> 


       [주의사항]
         -Google Cloud Translation API는 유료 서비스이므로 사용시 인지 확인
         월 50만 문자까지 무료 및 초과시 100만 문자당 이용료 부과
         -기계 번역이므로 완벽하지 않을 쑤 잇고, 중요한 내용은 변역 후, 검토 요망
         게임 용어나 고유 명사는 직접 검토 및 수정 확인
         -API를 통한 번역이므로 인터넷 연결 필수 및 방화벽에서 translation.googleapis.com 허용 필요

       [오류해결]
         [API 키 없음] : API 키 미설정
               -> Google Cloud에서 API 키 생성 후 입력
         [API 오류 401] : 잘못된 API 키
               -> API 키 재확인 및 재입력
         [API 오류 403] : API 권한 없음
               -> Translation API 활성화 확인
         [API 오류 429] : 사용량 초과
               -> 잠시 후 재시도 또는 할당량 확인
         [Newtonsoft.Json 관련 에러] : Package Manager에서 패키지 설치(의존성 패키지)
         [using문 에러] : Unity 2020.1 이상 사용 권장


       [파일 구조]
         (에디터 파일 위치) Assets/Editor/PatchNotesJsonGenerator.cs
         (패치노트 파일 폴더 위치) Assets/Resources/UpdateLogs
    

       [Json 파일 구조 예시]
       <img width="1191" height="747" alt="image" src="https://github.com/user-attachments/assets/92eb2424-dd6a-4468-a5f2-ecb3d625400e" />


    
    
    

  
    
