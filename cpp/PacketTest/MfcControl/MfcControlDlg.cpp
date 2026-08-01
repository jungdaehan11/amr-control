
// MfcControlDlg.cpp: 구현 파일
//

#include "pch.h"
#include "framework.h"
#include "MfcControl.h"
#include "MfcControlDlg.h"
#include "afxdialogex.h"

#ifdef _DEBUG
#define new DEBUG_NEW
#endif


// 응용 프로그램 정보에 사용되는 CAboutDlg 대화 상자입니다.

class CAboutDlg : public CDialogEx
{
public:
	CAboutDlg();

// 대화 상자 데이터입니다.
#ifdef AFX_DESIGN_TIME
	enum { IDD = IDD_ABOUTBOX };
#endif

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);    // DDX/DDV 지원입니다.

// 구현입니다.
protected:
	DECLARE_MESSAGE_MAP()
};

CAboutDlg::CAboutDlg() : CDialogEx(IDD_ABOUTBOX)
{
}

void CAboutDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
}

BEGIN_MESSAGE_MAP(CAboutDlg, CDialogEx)
END_MESSAGE_MAP()


// CMfcControlDlg 대화 상자



CMfcControlDlg::CMfcControlDlg(CWnd* pParent /*=nullptr*/)
	: CDialogEx(IDD_MFCCONTROL_DIALOG, pParent)
{
	m_hIcon = AfxGetApp()->LoadIcon(IDR_MAINFRAME);
}

void CMfcControlDlg::DoDataExchange(CDataExchange* pDX)
{
	CDialogEx::DoDataExchange(pDX);
	DDX_Control(pDX, IDC_EDIT_LOG, m_editLog);
	DDX_Control(pDX, IDC_STATIC_STATUS, m_editStatus);
}

BEGIN_MESSAGE_MAP(CMfcControlDlg, CDialogEx)
	ON_WM_SYSCOMMAND()
	ON_WM_PAINT()
	ON_WM_QUERYDRAGICON()
	ON_BN_CLICKED(IDC_BTN_FWD2, &CMfcControlDlg::OnBnClickedBtnFwd2)
	ON_BN_CLICKED(IDC_BTN_BACK, &CMfcControlDlg::OnBnClickedBtnBack)
	ON_BN_CLICKED(IDC_BTN_LEFT, &CMfcControlDlg::OnBnClickedBtnLeft)
	ON_BN_CLICKED(IDC_BTN_RIGHT, &CMfcControlDlg::OnBnClickedBtnRight)
	ON_BN_CLICKED(IDC_BTN_STOP, &CMfcControlDlg::OnBnClickedBtnStop)
	ON_BN_CLICKED(IDC_BTN_CONNECT, &CMfcControlDlg::OnBnClickedBtnConnect)
	ON_BN_CLICKED(IDC_BTN_CONNECT2, &CMfcControlDlg::OnBnClickedBtnConnect2)
END_MESSAGE_MAP()


// CMfcControlDlg 메시지 처리기

BOOL CMfcControlDlg::OnInitDialog()
{
	CDialogEx::OnInitDialog();

	// 시스템 메뉴에 "정보..." 메뉴 항목을 추가합니다.

	// IDM_ABOUTBOX는 시스템 명령 범위에 있어야 합니다.
	ASSERT((IDM_ABOUTBOX & 0xFFF0) == IDM_ABOUTBOX);
	ASSERT(IDM_ABOUTBOX < 0xF000);

	CMenu* pSysMenu = GetSystemMenu(FALSE);
	if (pSysMenu != nullptr)
	{
		BOOL bNameValid;
		CString strAboutMenu;
		bNameValid = strAboutMenu.LoadString(IDS_ABOUTBOX);
		ASSERT(bNameValid);
		if (!strAboutMenu.IsEmpty())
		{
			pSysMenu->AppendMenu(MF_SEPARATOR);
			pSysMenu->AppendMenu(MF_STRING, IDM_ABOUTBOX, strAboutMenu);
		}
	}

	// 이 대화 상자의 아이콘을 설정합니다.  응용 프로그램의 주 창이 대화 상자가 아닐 경우에는
	//  프레임워크가 이 작업을 자동으로 수행합니다.
	SetIcon(m_hIcon, TRUE);			// 큰 아이콘을 설정합니다.
	SetIcon(m_hIcon, FALSE);		// 작은 아이콘을 설정합니다.

	// Winsock 초기화
	WSADATA wsa;
	WSAStartup(MAKEWORD(2, 2), &wsa);
	m_sock = INVALID_SOCKET;
	m_connected = false;
	AppendLog(_T("프로그램 시작"));

	return TRUE;  // 포커스를 컨트롤에 설정하지 않으면 TRUE를 반환합니다.

	
}

void CMfcControlDlg::OnSysCommand(UINT nID, LPARAM lParam)
{
	if ((nID & 0xFFF0) == IDM_ABOUTBOX)
	{
		CAboutDlg dlgAbout;
		dlgAbout.DoModal();
	}
	else
	{
		CDialogEx::OnSysCommand(nID, lParam);
	}
}

// 대화 상자에 최소화 단추를 추가할 경우 아이콘을 그리려면
//  아래 코드가 필요합니다.  문서/뷰 모델을 사용하는 MFC 애플리케이션의 경우에는
//  프레임워크에서 이 작업을 자동으로 수행합니다.

void CMfcControlDlg::OnPaint()
{
	if (IsIconic())
	{
		CPaintDC dc(this); // 그리기를 위한 디바이스 컨텍스트입니다.

		SendMessage(WM_ICONERASEBKGND, reinterpret_cast<WPARAM>(dc.GetSafeHdc()), 0);

		// 클라이언트 사각형에서 아이콘을 가운데에 맞춥니다.
		int cxIcon = GetSystemMetrics(SM_CXICON);
		int cyIcon = GetSystemMetrics(SM_CYICON);
		CRect rect;
		GetClientRect(&rect);
		int x = (rect.Width() - cxIcon + 1) / 2;
		int y = (rect.Height() - cyIcon + 1) / 2;

		// 아이콘을 그립니다.
		dc.DrawIcon(x, y, m_hIcon);
	}
	else
	{
		CDialogEx::OnPaint();
	}
}

// 사용자가 최소화된 창을 끄는 동안에 커서가 표시되도록 시스템에서
//  이 함수를 호출합니다.
HCURSOR CMfcControlDlg::OnQueryDragIcon()
{
	return static_cast<HCURSOR>(m_hIcon);
}


void CMfcControlDlg::OnBnClickedBtnFwd2()
{
	SendCmd(CMD_MOVE_FWD);
}
void CMfcControlDlg::AppendLog(const CString& msg)
{
	CString cur;
	m_editLog.GetWindowText(cur);
	cur += msg + _T("\r\n");
	m_editLog.SetWindowText(cur);
	m_editLog.LineScroll(m_editLog.GetLineCount());
}
void CMfcControlDlg::OnBnClickedBtnBack()
{
	// TODO: 여기에 컨트롤 알림 처리기 코드를 추가합니다.
	SendCmd(CMD_MOVE_BACK);
}

void CMfcControlDlg::OnBnClickedBtnLeft()
{
	// TODO: 여기에 컨트롤 알림 처리기 코드를 추가합니다.
	SendCmd(CMD_MOVE_LEFT);
}

void CMfcControlDlg::OnBnClickedBtnRight()
{
	// TODO: 여기에 컨트롤 알림 처리기 코드를 추가합니다.
	SendCmd(CMD_MOVE_RIGHT);
}

void CMfcControlDlg::OnBnClickedBtnStop()
{
	// TODO: 여기에 컨트롤 알림 처리기 코드를 추가합니다.
	SendCmd(CMD_MOVE_STOP);
}

void CMfcControlDlg::OnBnClickedBtnConnect()
{
	// TODO: 여기에 컨트롤 알림 처리기 코드를 추가합니다.
	
	{
		if (m_connected) {
			AppendLog(_T("이미 연결됨"));
			return;
		}

		// 소켓 생성
		m_sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		if (m_sock == INVALID_SOCKET) {
			AppendLog(_T("소켓 생성 실패"));
			return;
		}

		// 서버 주소 (127.0.0.1:9000 = 내 PC의 EchoServer)
		sockaddr_in addr{};
		addr.sin_family = AF_INET;
		addr.sin_port = htons(9000);
		inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);

		// 연결 시도
		if (connect(m_sock, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
			AppendLog(_T("연결 실패 (서버 켜져 있나요?)"));
			closesocket(m_sock);
			m_sock = INVALID_SOCKET;
			return;
		}

		m_connected = true;
		AppendLog(_T("서버 연결 성공"));
		m_editStatus.SetWindowText(_T("상태: 연결됨"));
	}
}

void CMfcControlDlg::OnBnClickedBtnConnect2()
{
	if (!m_connected) {
		AppendLog(_T("연결 안 됨"));
		return;
	}
	closesocket(m_sock);
	m_sock = INVALID_SOCKET;
	m_connected = false;
	AppendLog(_T("연결 해제됨"));
	m_editStatus.SetWindowText(_T("상태: 연결 안 됨"));
}

void CMfcControlDlg::SendCmd(uint8_t cmd)
{
	if (!m_connected) {
		AppendLog(_T("연결 안 됨 - 먼저 연결하세요"));
		return;
	}

	// 데이터 없는 명령 패킷 생성
	std::vector<uint8_t> data;
	auto pkt = buildPacket(cmd, data);

	// 전송
	send(m_sock, (char*)pkt.data(), (int)pkt.size(), 0);

	// 에코 응답 수신
	uint8_t buf[256];
	int len = recv(m_sock, (char*)buf, sizeof(buf), 0);
	if (len > 0) {
		CString msg;
		msg.Format(_T("전송 CMD=0x%02X, 에코 %d바이트 수신"), cmd, len);
		AppendLog(msg);
	}
}
