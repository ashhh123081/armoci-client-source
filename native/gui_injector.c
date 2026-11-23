#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <tlhelp32.h>
#include <commdlg.h>

#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#define MAX_CANDIDATES 256
#define WINDOW_TITLE_CAPACITY 256
#define REFRESH_INTERVAL_MS 750
#define STATUS_CAPACITY 512
#define ROW_CAPACITY 640

#define WM_APP_INJECT_DONE (WM_APP + 1)

#define IDC_DLL_LABEL 1001
#define IDC_DLL_EDIT  1002
#define IDC_BROWSE    1003
#define IDC_LIST      1004
#define IDC_REFRESH   1005
#define IDC_INJECT    1006
#define IDC_QUIT      1007
#define IDC_STATUS    1008

typedef struct process_candidate {
    DWORD process_id;
    wchar_t executable[MAX_PATH];
    wchar_t title[WINDOW_TITLE_CAPACITY];
} process_candidate;

typedef struct window_search_context {
    process_candidate *candidates;
    size_t count;
} window_search_context;

typedef struct injection_request {
    HWND window;
    DWORD process_id;
    wchar_t dll_path[MAX_PATH];
} injection_request;

static process_candidate candidates[MAX_CANDIDATES];
static size_t candidate_count = 0;
static wchar_t dll_path[MAX_PATH] = L"";

static void set_status(HWND window, const wchar_t *text) {
    HWND status = GetDlgItem(window, IDC_STATUS);
    if (status != NULL) {
        SetWindowTextW(status, text);
    }
}

static void format_last_error(
        wchar_t *destination, size_t capacity, const wchar_t *operation) {
    DWORD error = GetLastError();
    wchar_t *message = NULL;
    FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER
                    | FORMAT_MESSAGE_FROM_SYSTEM
                    | FORMAT_MESSAGE_IGNORE_INSERTS,
            NULL, error, 0, (wchar_t *)&message, 0, NULL);
    swprintf(destination, capacity, L"%ls failed (%lu): %ls",
            operation, (unsigned long)error,
            message == NULL ? L"unknown error" : message);
    if (message != NULL) {
        LocalFree(message);
    }
}

static void set_error(wchar_t *error, size_t capacity, const wchar_t *text) {
    if (error != NULL && capacity != 0) {
        wcsncpy(error, text, capacity - 1);
        error[capacity - 1] = L'\0';
    }
}

static int absolute_existing_file(
        const wchar_t *input, wchar_t *output, DWORD capacity) {
    DWORD length = GetFullPathNameW(input, capacity, output, NULL);
    DWORD attributes;
    if (length == 0 || length >= capacity) {
        return 0;
    }
    attributes = GetFileAttributesW(output);
    return attributes != INVALID_FILE_ATTRIBUTES
            && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

static int default_dll_path(wchar_t *output, DWORD capacity) {
    DWORD length = GetModuleFileNameW(NULL, output, capacity);
    DWORD attributes;
    wchar_t *file_name;
    if (length == 0 || length >= capacity) {
        return 0;
    }
    file_name = wcsrchr(output, L'\\');
    file_name = file_name == NULL ? output : file_name + 1;
    if ((size_t)(file_name - output) + wcslen(L"Vape421Native.dll") + 1
            > capacity) {
        return 0;
    }
    wcscpy(file_name, L"Vape421Native.dll");
    attributes = GetFileAttributesW(output);
    return attributes != INVALID_FILE_ATTRIBUTES
            && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

static int is_java_process(const wchar_t *executable) {
    return _wcsicmp(executable, L"java.exe") == 0
            || _wcsicmp(executable, L"javaw.exe") == 0;
}

static BOOL CALLBACK capture_window_title(HWND window, LPARAM parameter) {
    window_search_context *context = (window_search_context *)parameter;
    wchar_t title[WINDOW_TITLE_CAPACITY];
    DWORD process_id = 0;
    size_t index;
    if (!IsWindowVisible(window) || GetWindowTextLengthW(window) == 0) {
        return TRUE;
    }
    GetWindowThreadProcessId(window, &process_id);
    if (process_id == 0
            || GetWindowTextW(window, title, WINDOW_TITLE_CAPACITY) == 0) {
        return TRUE;
    }
    for (index = 0; index < context->count; ++index) {
        process_candidate *candidate = &context->candidates[index];
        if (candidate->process_id == process_id && candidate->title[0] == L'\0') {
            wcscpy(candidate->title, title);
            break;
        }
    }
    return TRUE;
}

static int compare_candidates(const void *left, const void *right) {
    const process_candidate *a = (const process_candidate *)left;
    const process_candidate *b = (const process_candidate *)right;
    if (a->process_id < b->process_id) return -1;
    if (a->process_id > b->process_id) return 1;
    return 0;
}

static size_t enumerate_candidates(
        process_candidate *candidate_list, size_t capacity) {
    HANDLE snapshot;
    PROCESSENTRY32W entry;
    size_t count = 0;
    size_t read_index;
    size_t write_index = 0;
    window_search_context context;

    snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) {
        return 0;
    }
    memset(&entry, 0, sizeof(entry));
    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snapshot, &entry)) {
        do {
            if (count < capacity && is_java_process(entry.szExeFile)) {
                process_candidate *candidate = &candidate_list[count++];
                memset(candidate, 0, sizeof(*candidate));
                candidate->process_id = entry.th32ProcessID;
                wcsncpy(candidate->executable, entry.szExeFile, MAX_PATH - 1);
            }
        } while (Process32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);

    context.candidates = candidate_list;
    context.count = count;
    EnumWindows(capture_window_title, (LPARAM)&context);

    /* The selector is for game windows, so omit background JVMs without one. */
    for (read_index = 0; read_index < count; ++read_index) {
        if (candidate_list[read_index].title[0] != L'\0') {
            if (write_index != read_index) {
                candidate_list[write_index] = candidate_list[read_index];
            }
            ++write_index;
        }
    }
    qsort(candidate_list, write_index, sizeof(*candidate_list),
            compare_candidates);
    return write_index;
}

static uintptr_t remote_module_base(DWORD process_id, const wchar_t *module_name) {
    HANDLE snapshot;
    MODULEENTRY32W entry;
    uintptr_t result = 0;
    snapshot = CreateToolhelp32Snapshot(
            TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, process_id);
    if (snapshot == INVALID_HANDLE_VALUE) {
        return 0;
    }
    memset(&entry, 0, sizeof(entry));
    entry.dwSize = sizeof(entry);
    if (Module32FirstW(snapshot, &entry)) {
        do {
            if (_wcsicmp(entry.szModule, module_name) == 0) {
                result = (uintptr_t)entry.modBaseAddr;
                break;
            }
        } while (Module32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);
    return result;
}

static uintptr_t remote_module_by_path(
        DWORD process_id, const wchar_t *module_path) {
    HANDLE snapshot;
    MODULEENTRY32W entry;
    uintptr_t result = 0;
    snapshot = CreateToolhelp32Snapshot(
            TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, process_id);
    if (snapshot == INVALID_HANDLE_VALUE) {
        return 0;
    }
    memset(&entry, 0, sizeof(entry));
    entry.dwSize = sizeof(entry);
    if (Module32FirstW(snapshot, &entry)) {
        do {
            if (_wcsicmp(entry.szExePath, module_path) == 0) {
                result = (uintptr_t)entry.modBaseAddr;
                break;
            }
        } while (Module32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);
    return result;
}

static int require_x64_target(HANDLE process) {
    typedef BOOL (WINAPI *is_wow64_process2_fn)(HANDLE, USHORT *, USHORT *);
    is_wow64_process2_fn is_wow64_process2 =
            (is_wow64_process2_fn)GetProcAddress(
                    GetModuleHandleW(L"kernel32.dll"), "IsWow64Process2");
    if (is_wow64_process2 != NULL) {
        USHORT process_machine = IMAGE_FILE_MACHINE_UNKNOWN;
        USHORT native_machine = IMAGE_FILE_MACHINE_UNKNOWN;
        if (!is_wow64_process2(process, &process_machine, &native_machine)) {
            return 0;
        }
        return process_machine == IMAGE_FILE_MACHINE_UNKNOWN
                && native_machine == IMAGE_FILE_MACHINE_AMD64;
    }
    {
        BOOL wow64 = FALSE;
        if (!IsWow64Process(process, &wow64)) {
            return 0;
        }
        return !wow64 && sizeof(void *) == 8;
    }
}

/* Returns 1 on success, 2 when the DLL is already mapped, 0 on failure. */
static int inject_library(DWORD process_id, const wchar_t *path,
        wchar_t *error, size_t error_capacity) {
    HANDLE process = NULL;
    HANDLE thread = NULL;
    LPVOID remote_path = NULL;
    HMODULE local_kernel;
    FARPROC local_load_library;
    uintptr_t remote_kernel;
    uintptr_t load_library_offset;
    LPTHREAD_START_ROUTINE remote_load_library;
    SIZE_T path_bytes = (wcslen(path) + 1) * sizeof(wchar_t);
    SIZE_T written = 0;
    DWORD wait_result;
    int attempt;
    int result = 0;

    if (error != NULL && error_capacity != 0) {
        error[0] = L'\0';
    }
    if (remote_module_by_path(process_id, path) != 0) {
        return 2;
    }

    process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION
                    | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ,
            FALSE, process_id);
    if (process == NULL) {
        format_last_error(error, error_capacity, L"OpenProcess");
        goto cleanup;
    }
    if (!require_x64_target(process)) {
        set_error(error, error_capacity, L"Target process is not x64");
        goto cleanup;
    }
    remote_path = VirtualAllocEx(process, NULL, path_bytes,
            MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (remote_path == NULL) {
        format_last_error(error, error_capacity, L"VirtualAllocEx");
        goto cleanup;
    }
    if (!WriteProcessMemory(process, remote_path, path,
            path_bytes, &written) || written != path_bytes) {
        format_last_error(error, error_capacity, L"WriteProcessMemory");
        goto cleanup;
    }

    local_kernel = GetModuleHandleW(L"kernel32.dll");
    local_load_library = GetProcAddress(local_kernel, "LoadLibraryW");
    remote_kernel = remote_module_base(process_id, L"kernel32.dll");
    if (local_kernel == NULL || local_load_library == NULL || remote_kernel == 0) {
        set_error(error, error_capacity,
                L"Could not resolve remote kernel32!LoadLibraryW");
        goto cleanup;
    }
    load_library_offset = (uintptr_t)local_load_library - (uintptr_t)local_kernel;
    remote_load_library = (LPTHREAD_START_ROUTINE)(
            remote_kernel + load_library_offset);
    thread = CreateRemoteThread(process, NULL, 0, remote_load_library,
            remote_path, 0, NULL);
    if (thread == NULL) {
        format_last_error(error, error_capacity, L"CreateRemoteThread");
        goto cleanup;
    }
    wait_result = WaitForSingleObject(thread, 30000);
    if (wait_result != WAIT_OBJECT_0) {
        set_error(error, error_capacity,
                L"Remote LoadLibraryW did not finish within 30 seconds");
        goto cleanup;
    }
    for (attempt = 0; attempt < 100; ++attempt) {
        if (remote_module_by_path(process_id, path) != 0) {
            result = 1;
            break;
        }
        Sleep(50);
    }
    if (result == 0) {
        set_error(error, error_capacity,
                L"LoadLibraryW returned, but the DLL is not mapped "
                L"(inspect vape421-native.log)");
        goto cleanup;
    }

cleanup:
    if (thread != NULL) CloseHandle(thread);
    if (remote_path != NULL && process != NULL) {
        VirtualFreeEx(process, remote_path, 0, MEM_RELEASE);
    }
    if (process != NULL) CloseHandle(process);
    return result;
}

static DWORD WINAPI injection_thread(void *parameter) {
    injection_request *request = (injection_request *)parameter;
    wchar_t error[STATUS_CAPACITY];
    wchar_t *message;
    int result;

    error[0] = L'\0';
    result = inject_library(request->process_id, request->dll_path,
            error, STATUS_CAPACITY);
    message = malloc(STATUS_CAPACITY * sizeof(wchar_t));
    if (message != NULL) {
        if (result == 1) {
            swprintf(message, STATUS_CAPACITY,
                    L"Loaded into PID %lu; the Java bootstrap is running "
                    L"asynchronously. Inspect vape421-native.log.",
                    (unsigned long)request->process_id);
        } else if (result == 2) {
            swprintf(message, STATUS_CAPACITY,
                    L"PID %lu already has this DLL loaded; "
                    L"no second bootstrap was requested.",
                    (unsigned long)request->process_id);
        } else if (error[0] != L'\0') {
            swprintf(message, STATUS_CAPACITY, L"Injection into PID %lu "
                    L"failed. %ls", (unsigned long)request->process_id, error);
        } else {
            swprintf(message, STATUS_CAPACITY,
                    L"Injection into PID %lu failed.",
                    (unsigned long)request->process_id);
        }
    }
    PostMessageW(request->window, WM_APP_INJECT_DONE,
            (WPARAM)request->process_id, (LPARAM)message);
    free(request);
    return 0;
}

static void apply_gui_font(HWND control) {
    static HFONT font;
    if (font == NULL) {
        font = (HFONT)GetStockObject(DEFAULT_GUI_FONT);
    }
    SendMessageW(control, WM_SETFONT, (WPARAM)font, MAKELPARAM(TRUE, 0));
}

static HWND create_control(HWND parent, const wchar_t *class_name,
        const wchar_t *text, DWORD style, DWORD extended_style,
        int x, int y, int width, int height, int identifier) {
    HWND control = CreateWindowExW(extended_style, class_name, text,
            WS_CHILD | WS_VISIBLE | style, x, y, width, height,
            parent, (HMENU)(INT_PTR)identifier, GetModuleHandleW(NULL), NULL);
    if (control != NULL) {
        apply_gui_font(control);
    }
    return control;
}

static void create_controls(HWND window) {
    create_control(window, L"STATIC", L"DLL:", SS_LEFT, 0,
            12, 15, 36, 20, IDC_DLL_LABEL);
    create_control(window, L"EDIT", dll_path,
            ES_AUTOHSCROLL | ES_READONLY, WS_EX_CLIENTEDGE,
            52, 12, 496, 22, IDC_DLL_EDIT);
    create_control(window, L"BUTTON", L"Browse...", BS_PUSHBUTTON, 0,
            556, 10, 76, 26, IDC_BROWSE);
    create_control(window, L"LISTBOX", L"",
            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT | WS_VSCROLL | WS_BORDER,
            WS_EX_CLIENTEDGE, 12, 44, 620, 320, IDC_LIST);
    create_control(window, L"BUTTON", L"Refresh", BS_PUSHBUTTON, 0,
            12, 372, 96, 28, IDC_REFRESH);
    create_control(window, L"BUTTON", L"Inject", BS_DEFPUSHBUTTON, 0,
            116, 372, 120, 28, IDC_INJECT);
    create_control(window, L"BUTTON", L"Quit", BS_PUSHBUTTON, 0,
            244, 372, 76, 28, IDC_QUIT);
    create_control(window, L"STATIC", L"", SS_LEFT, 0,
            12, 408, 620, 20, IDC_STATUS);
}

static void refresh_processes(HWND window) {
    HWND list = GetDlgItem(window, IDC_LIST);
    LRESULT selection = SendMessageW(list, LB_GETCURSEL, 0, 0);
    DWORD previous_id = 0;
    int restore = -1;
    size_t index;
    wchar_t row[ROW_CAPACITY];

    if (selection != LB_ERR && (size_t)selection < candidate_count) {
        previous_id = candidates[selection].process_id;
    }
    candidate_count = enumerate_candidates(candidates, MAX_CANDIDATES);
    SendMessageW(list, LB_RESETCONTENT, 0, 0);
    for (index = 0; index < candidate_count; ++index) {
        swprintf(row, ROW_CAPACITY, L"[%lu] %-9ls  %ls",
                (unsigned long)candidates[index].process_id,
                candidates[index].executable, candidates[index].title);
        SendMessageW(list, LB_ADDSTRING, 0, (LPARAM)row);
        if (candidates[index].process_id == previous_id) {
            restore = (int)index;
        }
    }
    if (restore >= 0) {
        SendMessageW(list, LB_SETCURSEL, (WPARAM)restore, 0);
    }
    if (candidate_count == 0) {
        set_status(window,
                L"No visible java.exe/javaw.exe windows yet; waiting...");
    }
}

static void browse_for_dll(HWND window) {
    OPENFILENAMEW dialog;
    wchar_t file[MAX_PATH] = L"";
    wchar_t absolute[MAX_PATH];
    memset(&dialog, 0, sizeof(dialog));
    dialog.lStructSize = sizeof(dialog);
    dialog.hwndOwner = window;
    dialog.lpstrFilter = L"DLL files (*.dll)\0*.dll\0All files (*.*)\0*.*\0";
    dialog.lpstrFile = file;
    dialog.nMaxFile = MAX_PATH;
    dialog.lpstrTitle = L"Select Vape421Native.dll";
    dialog.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
    if (GetOpenFileNameW(&dialog)
            && absolute_existing_file(file, absolute, MAX_PATH)) {
        wcscpy(dll_path, absolute);
        SetWindowTextW(GetDlgItem(window, IDC_DLL_EDIT), dll_path);
        set_status(window, L"DLL selected.");
    }
}

static void start_injection(HWND window) {
    HWND list = GetDlgItem(window, IDC_LIST);
    LRESULT selection;
    injection_request *request;
    wchar_t status[STATUS_CAPACITY];

    if (!IsWindowEnabled(GetDlgItem(window, IDC_INJECT))) {
        return;
    }
    selection = SendMessageW(list, LB_GETCURSEL, 0, 0);
    if (selection == LB_ERR || (size_t)selection >= candidate_count) {
        set_status(window, L"Select a Java process first.");
        return;
    }
    if (dll_path[0] == L'\0'
            || !absolute_existing_file(dll_path, dll_path, MAX_PATH)) {
        set_status(window,
                L"The selected DLL does not exist; use Browse...");
        return;
    }
    request = malloc(sizeof(*request));
    if (request == NULL) {
        set_status(window, L"Out of memory.");
        return;
    }
    request->window = window;
    request->process_id = candidates[selection].process_id;
    wcscpy(request->dll_path, dll_path);
    EnableWindow(GetDlgItem(window, IDC_INJECT), FALSE);
    EnableWindow(GetDlgItem(window, IDC_REFRESH), FALSE);
    swprintf(status, STATUS_CAPACITY, L"Injecting into PID %lu...",
            (unsigned long)request->process_id);
    set_status(window, status);
    if (CreateThread(NULL, 0, injection_thread, request, 0, NULL) == NULL) {
        free(request);
        EnableWindow(GetDlgItem(window, IDC_INJECT), TRUE);
        EnableWindow(GetDlgItem(window, IDC_REFRESH), TRUE);
        set_status(window, L"CreateThread failed.");
    }
}

static LRESULT CALLBACK window_procedure(
        HWND window, UINT message, WPARAM wParam, LPARAM lParam) {
    switch (message) {
    case WM_CREATE:
        create_controls(window);
        SetTimer(window, 1, REFRESH_INTERVAL_MS, NULL);
        refresh_processes(window);
        if (dll_path[0] == L'\0') {
            set_status(window,
                    L"Vape421Native.dll was not found beside the injector; "
                    L"use Browse...");
        } else {
            set_status(window, L"Ready. Select a window and press Inject.");
        }
        return 0;
    case WM_TIMER:
        if (wParam == 1) {
            refresh_processes(window);
        }
        return 0;
    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case IDC_BROWSE:
            browse_for_dll(window);
            return 0;
        case IDC_REFRESH:
            refresh_processes(window);
            return 0;
        case IDC_INJECT:
            start_injection(window);
            return 0;
        case IDC_QUIT:
            DestroyWindow(window);
            return 0;
        default:
            break;
        }
        if (LOWORD(wParam) == IDC_LIST && HIWORD(wParam) == LBN_DBLCLK) {
            start_injection(window);
            return 0;
        }
        break;
    case WM_APP_INJECT_DONE: {
        const wchar_t *posted = (const wchar_t *)lParam;
        wchar_t fallback[STATUS_CAPACITY];
        if (posted == NULL) {
            swprintf(fallback, STATUS_CAPACITY,
                    L"Injection into PID %lu finished with an unknown result.",
                    (unsigned long)wParam);
            posted = fallback;
        }
        set_status(window, posted);
        if (posted != (const wchar_t *)lParam) {
            /* fallback was used; nothing to release */
        } else {
            free((void *)lParam);
        }
        EnableWindow(GetDlgItem(window, IDC_INJECT), TRUE);
        EnableWindow(GetDlgItem(window, IDC_REFRESH), TRUE);
        refresh_processes(window);
        return 0;
    }
    case WM_CTLCOLORSTATIC:
        SetBkColor((HDC)wParam, GetSysColor(COLOR_BTNFACE));
        return (LRESULT)GetSysColorBrush(COLOR_BTNFACE);
    case WM_DESTROY:
        KillTimer(window, 1);
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }
    return DefWindowProcW(window, message, wParam, lParam);
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE previous,
        PWSTR command_line, int show_command) {
    WNDCLASSW window_class;
    HWND window;
    MSG message;

    (void)previous;
    (void)command_line;

    if (!default_dll_path(dll_path, MAX_PATH)) {
        dll_path[0] = L'\0';
    }
    memset(&window_class, 0, sizeof(window_class));
    window_class.lpfnWndProc = window_procedure;
    window_class.hInstance = instance;
    window_class.hCursor = LoadCursorW(NULL, IDC_ARROW);
    window_class.hbrBackground = (HBRUSH)(COLOR_BTNFACE + 1);
    window_class.lpszClassName = L"Vape421GuiInjector";
    RegisterClassW(&window_class);

    window = CreateWindowExW(0, window_class.lpszClassName,
            L"Vape421 GUI Injector",
            WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
            CW_USEDEFAULT, CW_USEDEFAULT, 656, 470,
            NULL, NULL, instance, NULL);
    if (window == NULL) {
        return 1;
    }
    ShowWindow(window, show_command);
    UpdateWindow(window);

    while (GetMessageW(&message, NULL, 0, 0) > 0) {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }
    return (int)message.wParam;
}
