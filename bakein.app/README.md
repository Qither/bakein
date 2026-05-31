# Bakein App

Taro frontend for the Bakein mini program.

## WeChat Registration Smoke

The profile page uses WeChat mini program capabilities for quick registration:

- `Taro.login()` obtains the short-lived code that the backend exchanges with WeChat `jscode2session`.
- `Button openType='chooseAvatar'` and `Input type='nickname'` collect the avatar and nickname the user selected in WeChat.
- `POST /api/auth/wechat/register` returns the Bakein bearer token, then `/api/users/me/profile` returns `wechatIdentity` for display on the profile page.

For WeChat DevTools or a real device, build with an API URL the mini program can reach. `http://localhost:5164` only works for local simulator-style smoke tests.

PowerShell example:

```powershell
$env:TARO_APP_API_BASE_URL='https://your-api.example.com'
npm run build:weapp
```

Local backend mock smoke is opt-in and should not be used for real registration:

```powershell
cd ..\bakein.api
$env:WECHAT_USE_MOCK_SESSION='true'
docker compose up -d api

cd ..\bakein.app
$env:WECHAT_SMOKE='true'
npm run test:api
```
