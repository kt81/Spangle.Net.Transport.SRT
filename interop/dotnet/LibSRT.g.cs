// <auto-generated>
// This code is generated by csbindgen.
// DON'T CHANGE THIS DIRECTLY.
// </auto-generated>
#pragma warning disable CS8981
using System;
using System.Runtime.InteropServices;

namespace Spangle.Interop.Native
{
    internal static unsafe partial class LibSRT
    {
        const string __DllName = "libsrt-interop";

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_startup", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_startup();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_cleanup", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_cleanup();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_socket", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_socket(int arg1, int arg2, int arg3);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_create_socket", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_create_socket();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_bind", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_bind(int u, sockaddr* name, int namelen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_bind_acquire", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_bind_acquire(int u, int sys_udp_sock);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_listen", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_listen(int u, int backlog);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_accept", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_accept(int u, sockaddr* addr, int* addrlen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_accept_bond", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_accept_bond(int* listeners, int lsize, long msTimeOut);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_listen_callback", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_listen_callback(int lsn, delegate* unmanaged[Cdecl]<void*, int, int, sockaddr*, byte*, int> hook_fn, void* hook_opaque);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_connect_callback", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_connect_callback(int clr, delegate* unmanaged[Cdecl]<void*, int, int, sockaddr*, int, void> hook_fn, void* hook_opaque);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_connect", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_connect(int u, sockaddr* name, int namelen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_connect_debug", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_connect_debug(int u, sockaddr* name, int namelen, int forced_isn);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_connect_bind", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_connect_bind(int u, sockaddr* source, sockaddr* target, int len);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_rendezvous", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_rendezvous(int u, sockaddr* local_name, int local_namelen, sockaddr* remote_name, int remote_namelen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_close", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_close(int u);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getpeername", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getpeername(int u, sockaddr* name, int* namelen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getsockname", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getsockname(int u, sockaddr* name, int* namelen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getsockopt", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getsockopt(int u, int level, uint optname, void* optval, int* optlen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_setsockopt", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_setsockopt(int u, int level, uint optname, void* optval, int optlen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getsockflag", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getsockflag(int u, uint opt, void* optval, int* optlen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_setsockflag", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_setsockflag(int u, uint opt, void* optval, int optlen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_msgctrl_init", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_msgctrl_init(SRT_MsgCtrl_* mctrl);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_send", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_send(int u, byte* buf, int len);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_sendmsg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_sendmsg(int u, byte* buf, int len, int ttl, int inorder);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_sendmsg2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_sendmsg2(int u, byte* buf, int len, SRT_MsgCtrl_* mctrl);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_recv", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_recv(int u, byte* buf, int len);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_recvmsg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_recvmsg(int u, byte* buf, int len);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_recvmsg2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_recvmsg2(int u, byte* buf, int len, SRT_MsgCtrl_* mctrl);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_sendfile", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern long srt_sendfile(int u, byte* path, long* offset, long size, int block);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_recvfile", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern long srt_recvfile(int u, byte* path, long* offset, long size, int block);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getlasterror_str", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern byte* srt_getlasterror_str();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getlasterror", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getlasterror(int* errno_loc);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_strerror", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern byte* srt_strerror(int code, int errnoval);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_clearlasterror", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_clearlasterror();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_bstats", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_bstats(int u, CBytePerfMon* perf, int clear);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_bistats", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_bistats(int u, CBytePerfMon* perf, int clear, int instantaneous);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getsockstate", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern uint srt_getsockstate(int u);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_create", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_create();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_clear_usocks", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_clear_usocks(int eid);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_add_usock", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_add_usock(int eid, int u, int* events);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_add_ssock", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_add_ssock(int eid, int s, int* events);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_remove_usock", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_remove_usock(int eid, int u);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_remove_ssock", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_remove_ssock(int eid, int s);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_update_usock", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_update_usock(int eid, int u, int* events);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_update_ssock", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_update_ssock(int eid, int s, int* events);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_wait", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_wait(int eid, int* readfds, int* rnum, int* writefds, int* wnum, long msTimeOut, int* lrfds, int* lrnum, int* lwfds, int* lwnum);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_uwait", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_uwait(int eid, SRT_EPOLL_EVENT_STR* fdsSet, int fdsSize, long msTimeOut);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_set", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_set(int eid, int flags);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_epoll_release", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_epoll_release(int eid);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_setloglevel", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_setloglevel(int ll);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_addlogfa", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_addlogfa(int fa);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_dellogfa", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_dellogfa(int fa);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_resetlogfa", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_resetlogfa(int* fara, nuint fara_size);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_setloghandler", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_setloghandler(void* opaque, delegate* unmanaged[Cdecl]<void*, int, byte*, int, byte*, byte*, void> handler);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_setlogflags", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_setlogflags(int flags);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getsndbuffer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getsndbuffer(int sock, nuint* blocks, nuint* bytes);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getrejectreason", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_getrejectreason(int sock);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_setrejectreason", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_setrejectreason(int sock, int value);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_rejectreason_str", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern byte* srt_rejectreason_str(int id);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_getversion", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern uint srt_getversion();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_time_now", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern long srt_time_now();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_connection_time", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern long srt_connection_time(int sock);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_clock_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_clock_type();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_create_group", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_create_group(uint arg1);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_groupof", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_groupof(int socket);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_group_data", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_group_data(int socketgroup, SRT_SocketGroupData_* output, nuint* inoutlen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_create_config", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SRT_SocketOptionObject* srt_create_config();

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_delete_config", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void srt_delete_config(SRT_SocketOptionObject* config);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_config_add", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_config_add(SRT_SocketOptionObject* config, uint option, void* contents, int len);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_prepare_endpoint", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SRT_GroupMemberConfig_ srt_prepare_endpoint(sockaddr* src, sockaddr* adr, int namelen);

        [DllImport(__DllName, EntryPoint = "csbindgen_srt_connect_group", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int srt_connect_group(int group, SRT_GroupMemberConfig_* name, int arraysize);


    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct sockaddr
    {
        public ushort sa_family;
        public fixed byte sa_data[14];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct sockaddr_storage
    {
        public ushort ss_family;
        public fixed byte __ss_padding[118];
        public CULong __ss_align;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct CBytePerfMon
    {
        public long msTimeStamp;
        public long pktSentTotal;
        public long pktRecvTotal;
        public int pktSndLossTotal;
        public int pktRcvLossTotal;
        public int pktRetransTotal;
        public int pktSentACKTotal;
        public int pktRecvACKTotal;
        public int pktSentNAKTotal;
        public int pktRecvNAKTotal;
        public long usSndDurationTotal;
        public int pktSndDropTotal;
        public int pktRcvDropTotal;
        public int pktRcvUndecryptTotal;
        public ulong byteSentTotal;
        public ulong byteRecvTotal;
        public ulong byteRcvLossTotal;
        public ulong byteRetransTotal;
        public ulong byteSndDropTotal;
        public ulong byteRcvDropTotal;
        public ulong byteRcvUndecryptTotal;
        public long pktSent;
        public long pktRecv;
        public int pktSndLoss;
        public int pktRcvLoss;
        public int pktRetrans;
        public int pktRcvRetrans;
        public int pktSentACK;
        public int pktRecvACK;
        public int pktSentNAK;
        public int pktRecvNAK;
        public double mbpsSendRate;
        public double mbpsRecvRate;
        public long usSndDuration;
        public int pktReorderDistance;
        public double pktRcvAvgBelatedTime;
        public long pktRcvBelated;
        public int pktSndDrop;
        public int pktRcvDrop;
        public int pktRcvUndecrypt;
        public ulong byteSent;
        public ulong byteRecv;
        public ulong byteRcvLoss;
        public ulong byteRetrans;
        public ulong byteSndDrop;
        public ulong byteRcvDrop;
        public ulong byteRcvUndecrypt;
        public double usPktSndPeriod;
        public int pktFlowWindow;
        public int pktCongestionWindow;
        public int pktFlightSize;
        public double msRTT;
        public double mbpsBandwidth;
        public int byteAvailSndBuf;
        public int byteAvailRcvBuf;
        public double mbpsMaxBW;
        public int byteMSS;
        public int pktSndBuf;
        public int byteSndBuf;
        public int msSndBuf;
        public int msSndTsbPdDelay;
        public int pktRcvBuf;
        public int byteRcvBuf;
        public int msRcvBuf;
        public int msRcvTsbPdDelay;
        public int pktSndFilterExtraTotal;
        public int pktRcvFilterExtraTotal;
        public int pktRcvFilterSupplyTotal;
        public int pktRcvFilterLossTotal;
        public int pktSndFilterExtra;
        public int pktRcvFilterExtra;
        public int pktRcvFilterSupply;
        public int pktRcvFilterLoss;
        public int pktReorderTolerance;
        public long pktSentUniqueTotal;
        public long pktRecvUniqueTotal;
        public ulong byteSentUniqueTotal;
        public ulong byteRecvUniqueTotal;
        public long pktSentUnique;
        public long pktRecvUnique;
        public ulong byteSentUnique;
        public ulong byteRecvUnique;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct SRT_MsgCtrl_
    {
        public int flags;
        public int msgttl;
        public int inorder;
        public int boundary;
        public long srctime;
        public int pktseq;
        public int msgno;
        public SRT_SocketGroupData_* grpdata;
        public nuint grpdata_size;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct SRT_EPOLL_EVENT_STR
    {
        public int fd;
        public int events;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct SRT_SocketGroupData_
    {
        public int id;
        public sockaddr_storage peeraddr;
        public uint sockstate;
        public ushort weight;
        public uint memberstate;
        public int result;
        public int token;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct SRT_SocketOptionObject
    {
        public fixed byte _unused[1];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe partial struct SRT_GroupMemberConfig_
    {
        public int id;
        public sockaddr_storage srcaddr;
        public sockaddr_storage peeraddr;
        public ushort weight;
        public SRT_SocketOptionObject* config;
        public int errorcode;
        public int token;
    }



}
    