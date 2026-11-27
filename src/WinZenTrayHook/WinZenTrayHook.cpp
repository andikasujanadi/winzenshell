#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <stddef.h>
#include <stdlib.h>
#include <stdio.h>

static const wchar_t* PIPE_NAME = L"\\\\.\\pipe\\WinZenTrayHookPipe";
static const wchar_t* STOP_EVENT_NAME = L"Local\\WinZenTrayHookStopEvent";
static const DWORD SHELLTRAY_SIGNATURE = 0x34753423;

#pragma pack(push, 1)
struct NOTIFYICONDATA32_WIRE
{
    DWORD cbSize;
    DWORD hWnd;
    UINT uID;
    UINT uFlags;
    UINT uCallbackMessage;
    DWORD hIcon;
    WCHAR szTip[128];
    DWORD dwState;
    DWORD dwStateMask;
    WCHAR szInfo[256];
    UINT uTimeoutOrVersion;
    WCHAR szInfoTitle[64];
    DWORD dwInfoFlags;
    GUID guidItem;
    DWORD hBalloonIcon;
};

struct SHELLTRAYDATA_WIRE
{
    DWORD dwSignature;
    DWORD dwMessage;
    NOTIFYICONDATA32_WIRE nid;
};
#pragma pack(pop)

struct TrayPipePacket
{
    DWORD dwMessage;
    DWORD processId;
    UINT64 hWnd;
    UINT uID;
    UINT uFlags;
    UINT uCallbackMessage;
    UINT uVersion;
    WCHAR szTip[128];
};

static HMODULE g_hModule = NULL;
static HWND g_hTrayWnd = NULL;
static WNDPROC g_origWndProc = NULL;
static HANDLE g_hWorkerThread = NULL;
static CRITICAL_SECTION g_cs;
static HANDLE g_hPipe = INVALID_HANDLE_VALUE;

static void SendPacketToWinZen(const TrayPipePacket& pkt)
{
    EnterCriticalSection(&g_cs);
    if (g_hPipe == INVALID_HANDLE_VALUE)
    {
        g_hPipe = CreateFileW(
            PIPE_NAME,
            GENERIC_WRITE,
            0,
            NULL,
            OPEN_EXISTING,
            0,
            NULL);
    }

    if (g_hPipe != INVALID_HANDLE_VALUE)
    {
        DWORD written = 0;
        if (!WriteFile(g_hPipe, &pkt, sizeof(pkt), &written, NULL) || written != sizeof(pkt))
        {
            CloseHandle(g_hPipe);
            g_hPipe = INVALID_HANDLE_VALUE;
        }
    }
    LeaveCriticalSection(&g_cs);
}

static LRESULT CALLBACK HookedTrayWndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    if (uMsg == WM_COPYDATA && lParam != 0)
    {
        const COPYDATASTRUCT* cds = reinterpret_cast<const COPYDATASTRUCT*>(lParam);
        if (cds && cds->dwData == 1 && cds->lpData != NULL && cds->cbData >= 8 + 24)
        {
            const SHELLTRAYDATA_WIRE* data = reinterpret_cast<const SHELLTRAYDATA_WIRE*>(cds->lpData);
            if (data->dwSignature == SHELLTRAY_SIGNATURE)
            {
                HWND iconHwnd = reinterpret_cast<HWND>(static_cast<ULONG_PTR>(data->nid.hWnd));
                DWORD pid = 0;
                if (iconHwnd && IsWindow(iconHwnd))
                {
                    GetWindowThreadProcessId(iconHwnd, &pid);
                }

                TrayPipePacket pkt = {};
                pkt.dwMessage = data->dwMessage;
                pkt.processId = pid;
                pkt.hWnd = static_cast<UINT64>(data->nid.hWnd);
                pkt.uID = data->nid.uID;
                pkt.uFlags = data->nid.uFlags;
                pkt.uCallbackMessage = data->nid.uCallbackMessage;
                pkt.uVersion = (cds->cbData >= 8 + offsetof(NOTIFYICONDATA32_WIRE, szInfoTitle))
                    ? data->nid.uTimeoutOrVersion
                    : 0;

                if ((data->nid.uFlags & NIF_TIP) != 0 && cds->cbData >= 8 + offsetof(NOTIFYICONDATA32_WIRE, dwState))
                {
                    wcsncpy_s(pkt.szTip, _countof(pkt.szTip), data->nid.szTip, _TRUNCATE);
                }

                SendPacketToWinZen(pkt);
            }
        }
    }

    return CallWindowProcW(g_origWndProc, hwnd, uMsg, wParam, lParam);
}

static DWORD WINAPI WorkerThreadProc(LPVOID)
{
    HWND hTray = FindWindowW(L"Shell_TrayWnd", NULL);
    if (hTray != NULL)
    {
        g_hTrayWnd = hTray;
        g_origWndProc = reinterpret_cast<WNDPROC>(GetWindowLongPtrW(hTray, GWLP_WNDPROC));
        SetWindowLongPtrW(hTray, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(HookedTrayWndProc));
    }

    HANDLE hStop = CreateEventW(NULL, TRUE, FALSE, STOP_EVENT_NAME);
    if (hStop != NULL)
    {
        WaitForSingleObject(hStop, INFINITE);
        CloseHandle(hStop);
    }
    else
    {
        Sleep(INFINITE);
    }

    if (g_hTrayWnd != NULL && g_origWndProc != NULL && IsWindow(g_hTrayWnd))
    {
        SetWindowLongPtrW(g_hTrayWnd, GWLP_WNDPROC, reinterpret_cast<LONG_PTR>(g_origWndProc));
        g_origWndProc = NULL;
    }

    EnterCriticalSection(&g_cs);
    if (g_hPipe != INVALID_HANDLE_VALUE)
    {
        CloseHandle(g_hPipe);
        g_hPipe = INVALID_HANDLE_VALUE;
    }
    LeaveCriticalSection(&g_cs);

    FreeLibraryAndExitThread(g_hModule, 0);
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        InitializeCriticalSection(&g_cs);
        g_hWorkerThread = CreateThread(NULL, 0, WorkerThreadProc, NULL, 0, NULL);
        if (g_hWorkerThread) CloseHandle(g_hWorkerThread);
        break;
    case DLL_PROCESS_DETACH:
        DeleteCriticalSection(&g_cs);
        break;
    }
    return TRUE;
}
