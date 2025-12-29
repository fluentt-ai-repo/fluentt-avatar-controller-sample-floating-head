# FluentT Avatar Controller Sample (Floating Head)

FluentT Talkmotion 시스템용 샘플 아바타 컨트롤러 구현 (플로팅 헤드 버전)입니다.

## 기능

- **눈 깜박임 애니메이션**: 무작위 간격으로 자동 눈 깜박임
- **시선 타겟 제어**: 가상 타겟과 가중치 블렌딩을 사용한 동적 시선 제어
- **감정 태깅**: 텍스트 기반 감정 감지 및 얼굴 표정 제어
- **서버 모션 태깅**: 서버 기반 모션 태그 응답
- **바디 애니메이션**: 상태 머신 기반 바디 애니메이션 시스템

## 설치

### Package Manager를 통한 설치 (Git URL)

1. Unity Package Manager 열기 (Window > Package Manager)
2. `+` 버튼 클릭 → Add package from git URL
3. 입력: `https://github.com/Fluentt-ai/fluentt-avatar-controller-sample.git`

### manifest.json을 통한 설치

프로젝트의 `Packages/manifest.json`에 추가:

```json
{
  "dependencies": {
    "com.fluentt.avatar-controller-sample": "https://github.com/Fluentt-ai/fluentt-avatar-controller-sample.git"
  }
}
```

## 요구 사항

- Unity 2022.3 이상
- FluentT Talkmotion SDK (`com.fluentt.talkmotion`)

## 사용법

1. 아바타 GameObject에 `FluentTAvatarControllerFloatingHead` 컴포넌트 추가
2. Inspector에서 아바타 참조 및 설정 구성
3. 컨트롤러가 자동으로 FluentTAvatar 컴포넌트와 통합됨

## 문서

자세한 사용 가이드는 [Documentation](Documentation/README.md) 폴더를 참조하세요.

## 라이선스

Proprietary - FluentT AI
