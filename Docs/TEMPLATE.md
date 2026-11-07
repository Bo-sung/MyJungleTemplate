# 템플릿 패키지로 프로젝트 만들기

이 저장소를 유니티 템플릿(`.tgz`)으로 옮겨도 AGENTS.md·Docs가 따라가게 하는 방법. 실제 동작을 확인해 기록한다.

## 확인한 사실(Unity CLI 1.0.0-beta.16 · Unity 6000.3.22f1)
- `unity templates create <프로젝트>`는 템플릿 아카이브의 `ProjectData~`에 `Assets/`, `Packages/`, `ProjectSettings/`, `Library/`만 담는다. 저장소 루트에 둔 `AGENTS.md`·`README.md`·`Docs/`·`.gitignore`는 **빠진다**.
- `--keep-project-settings`를 빠뜨리면 `ProjectSettings` 자체가 사라져 레이어 이름(Ground~Projectile)과 Enter Play Mode Options(도메인 리로드 해제)가 기본값으로 돌아간다.
- `ProjectData~`의 내용은 새 프로젝트 **루트에 그대로 복사**된다. 루트에 둔 텍스트 파일도 예외 없이 복사된다(Probe 프로젝트에서 확인).
- 따라서 "루트 파일이 안 넘어간다"의 해법은 아카이브의 `ProjectData~`에 그 파일들을 직접 넣는 것이다.

## 만들기
`Tools/MyJG/Build Template Package`를 실행하면 두 단계가 자동으로 돈다(`Assets/Scripts/Editor/Template/TemplatePackTool.cs`).
1. `unity templates create`(기본은 `--keep-project-settings --overwrite`, 출력은 사용자 템플릿 폴더)
2. `tar`로 아카이브를 풀어 `package/ProjectData~/`에 루트 항목(`AGENTS.md`, `README.md`, `Docs/`, `.gitignore`, `.gitattributes`)을 넣고 다시 싼다

포함할 루트 항목은 `TemplatePackTool.ROOT_ITEMS`가 원본이다. 문서를 추가하면 이 목록에도 반영한다.

### 수동으로 할 때
```
unity templates create "<프로젝트 경로>" --name com.myjg.template.myjg --display-name "MyJG Template" --version 0.1.0 --keep-project-settings --overwrite
tar xzf "<tgz>" -C "<빈 폴더>"
# package/ProjectData~/ 에 위 루트 항목 복사
tar czf "<tgz>" -C "<빈 폴더>" package
```
재포장할 때 아카이브 최상위는 `package/`여야 한다(유니티가 기대하는 tgz 구조).

## 확인
```
unity templates --editor-version 6000.3.22f1
unity projects create Probe --path "<빈 폴더>" --editor-version 6000.3.22f1 --template com.myjg.template.myjg --no-cloud
```
- `unity templates` 목록에 `com.myjg.template.myjg`이 CUSTOM / ready로 뜨는지
- 생성된 프로젝트 루트에 `AGENTS.md`·`Docs`가 있는지
- `Assets/Scenes`에 Preload·Title·Game, `Assets/Resources/Tables` 두 개, `Packages/manifest.json`의 LogKit git 의존성이 유지되는지

## 참고
- 사용자 템플릿 저장 위치: `%USERPROFILE%\Unity user templates\<slug>`(slug는 패키지 이름에서 점을 뺀 형태). `unity templates location`으로 확인·변경한다.
- 아카이브에 `Library`가 들어가 첫 임포트가 빠르지만 크기가 60MB 정도 나온다.
- 템플릿 id·version을 올리면 아카이브를 다시 만들어 등록해야 한다.
