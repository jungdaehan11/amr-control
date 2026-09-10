
// MfcControlDlg.h: 헤더 파일
//

#pragma once
#include <winsock2.h>
#include <ws2tcpip.h>
#include "Packet.h"

#pragma comment(lib, "ws2_32.lib")
// CMfcControlDlg 대화 상자
class CMfcControlDlg : public CDialogEx
{
// 생성입니다.
public:
	CMfcControlDlg(CWnd* pParent = nullptr);	// 표준 생성자입니다.

// 대화 상자 데이터입니다.
#ifdef AFX_DESIGN_TIME
	enum { IDD = IDD_MFCCONTROL_DIALOG };
#endif

	protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV 지원입니다.


// 구현입니다.
protected:
	HICON m_hIcon;

	// 생성된 메시지 맵 함수
	virtual BOOL OnInitDialog();
	afx_msg void OnSysCommand(UINT nID, LPARAM lParam);
	afx_msg void OnPaint();
	afx_msg HCURSOR OnQueryDragIcon();
	DECLARE_MESSAGE_MAP()
public:
	afx_msg void OnBnClickedBtnFwd2();
	CEdit m_editLog;
	SOCKET m_sock;          // 서버 연결 소켓
	bool   m_connected;     // 연결 상태
	void AppendLog(const CString& msg);   // 로그 창에 한 줄 추가
	void SendCmd(uint8_t cmd);   // CMD 패킷 전송
	afx_msg void OnBnClickedBtnBack();
	afx_msg void OnBnClickedBtnLeft();
	afx_msg void OnBnClickedBtnRight();
	afx_msg void OnBnClickedBtnStop();
	afx_msg void OnBnClickedBtnConnect();
	afx_msg void OnBnClickedBtnConnect2();
	CStatic m_editStatus;
};
