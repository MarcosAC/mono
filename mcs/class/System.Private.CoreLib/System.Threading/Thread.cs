using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Threading
{
	//
	// Under netcore, there is only one thread object per thread
	//
	[StructLayout (LayoutKind.Sequential)]
	partial class Thread
	{
#pragma warning disable 169, 414, 649
		#region Sync with metadata/object-internals.h and InternalThread in mcs/class/corlib/System.Threading/Thread.cs
		int lock_thread_id;
		// stores a thread handle
		IntPtr handle;
		IntPtr native_handle; // used only on Win32
		IntPtr unused3;
		/* accessed only from unmanaged code */
		private IntPtr name;
		private int name_len;
		private ThreadState state;
		private object abort_exc;
		private int abort_state_handle;
		/* thread_id is only accessed from unmanaged code */
		internal Int64 thread_id;
		private IntPtr debugger_thread; // FIXME switch to bool as soon as CI testing with corlib version bump works
		private UIntPtr static_data; /* GC-tracked */
		private IntPtr runtime_thread_info;
		/* current System.Runtime.Remoting.Contexts.Context instance
		   keep as an object to avoid triggering its class constructor when not needed */
		private object current_appcontext;
		private object root_domain_thread;
		internal byte[] _serialized_principal;
		internal int _serialized_principal_version;
		private IntPtr appdomain_refs;
		private int interruption_requested;
		private IntPtr longlived;
		internal bool threadpool_thread;
		private bool thread_interrupt_requested;
		/* These are used from managed code */
		internal int stack_size;
		internal byte apartment_state;
		internal volatile int critical_region_level;
		internal int managed_id;
		private int small_id;
		private IntPtr manage_callback;
		private IntPtr unused4;
		private IntPtr flags;
		private IntPtr thread_pinning_ref;
		private IntPtr abort_protected_block_count;
		private int priority;
		private IntPtr owned_mutex;
		private IntPtr suspended_event;
		private int self_suspended;
		private IntPtr thread_state;
		private Thread self;
		private object pending_exception;
		private object start_obj;

		/* This is used only to check that we are in sync between the representation
		 * of MonoInternalThread in native and InternalThread in managed
		 *
		 * DO NOT RENAME! DO NOT ADD FIELDS AFTER! */
		private IntPtr last;
		#endregion
#pragma warning restore 169, 414, 649

		string m_name;
		Delegate m_start;
		object m_start_arg;
		int m_stacksize;
		internal ExecutionContext _executionContext;

		[ThreadStatic]
		static Thread current_thread;

		Thread ()
		{
      priority =  (int)ThreadPriority.Normal;
			InitInternal (this);
		}

		public ExecutionContext ExecutionContext => ExecutionContext.Capture ();

		public static Thread CurrentThread {
			get {
				Thread current = current_thread;
				if (current != null)
					return current;

				// This will set the current_thread tls variable
				return GetCurrentThread ();
			}
		}

		internal static ulong CurrentOSThreadId {
			get {
				throw new NotImplementedException ();
			}
		}

		public bool IsAlive {
			get {
				var state = GetState (this);
				return (state & (ThreadState.Unstarted | ThreadState.Stopped | ThreadState.Aborted)) == 0;
			}
		}

		public bool IsBackground {
			get {
				var state = ValidateThreadState ();
				return (state & ThreadState.Background) != 0;
			}
			set {
				ValidateThreadState ();
				if (value) {
					SetState (this, ThreadState.Background);
				} else {
					ClrState (this, ThreadState.Background);
				}
			}
		}

		public bool IsThreadPoolThread {
      get {
        ValidateThreadState ();
        return threadpool_thread;
      }
    }

		public int ManagedThreadId => managed_id;

		public string Name {
			get => m_name;
			set {
				lock (this) {
					if (m_name != null)
						throw new InvalidOperationException (SR.InvalidOperation_WriteOnce);

					m_name = value;
				}
				// This sets the native thread name etc.
				SetName (this, value);
			}
		}

		internal static int OptimalMaxSpinWaitsPerSpinIteration {
			get {
				throw new NotImplementedException ();
			}
		}

		public ThreadPriority Priority {
			get {
        ValidateThreadState ();
        return (ThreadPriority)priority;
      }
			set {
        // TODO: arguments check
        SetPriority (this, (int)value);
      }
		}

		internal SynchronizationContext SynchronizationContext { get; set; }

		public ThreadState ThreadState => GetState (this);

		void Create (ThreadStart start) => SetStartHelper ((Delegate)start, 0); // 0 will setup Thread with default stackSize

		void Create (ThreadStart start, int maxStackSize) => SetStartHelper ((Delegate)start, maxStackSize);

		void Create (ParameterizedThreadStart start) => SetStartHelper ((Delegate)start, 0);

		void Create (ParameterizedThreadStart start, int maxStackSize) => SetStartHelper ((Delegate)start, maxStackSize);

		public ApartmentState GetApartmentState () => ApartmentState.MTA;

		public void DisableComObjectEagerCleanup ()
		{
			// no-op
		}

		public static int GetCurrentProcessorId ()
		{
			throw new NotImplementedException ();
		}

		public void Interrupt ()
		{
			InterruptInternal (this);
		}

		public bool Join (int millisecondsTimeout)
		{
			if (millisecondsTimeout < Timeout.Infinite)
				throw new ArgumentOutOfRangeException (nameof (millisecondsTimeout), millisecondsTimeout, SR.ArgumentOutOfRange_NeedNonNegOrNegative1);

      ValidateThreadState ();

			return JoinInternal (this, millisecondsTimeout);
		}

		public void ResetThreadPoolThread ()
		{
		}

		void SetCultureOnUnstartedThreadNoCheck (CultureInfo value, bool uiCulture)
		{
			throw new NotImplementedException ();
		}

		void SetStartHelper (Delegate start, int maxStackSize)
		{
			m_start = start;
			m_stacksize = maxStackSize;
		}

		public static void SpinWait (int iterations)
		{
			if (iterations < 0)
				return;

			while (iterations-- > 0)
				SpinWait_nop ();
		}

		public static void Sleep (int millisecondsTimeout)
		{
			if (millisecondsTimeout < Timeout.Infinite)
				throw new ArgumentOutOfRangeException (nameof (millisecondsTimeout), millisecondsTimeout, SR.ArgumentOutOfRange_NeedNonNegOrNegative1);
      
      ValidateThreadState ();
      
			SleepInternal (millisecondsTimeout);
		}

		public void Start ()
		{
			StartInternal (this);
		}

		public void Start (object parameter)
		{
      if (m_start is ThreadStart)
        throw new InvalidOperationException (SR.InvalidOperation_ThreadWrongThreadStart);
      
			m_start_arg = parameter;
			StartInternal (this);
		}

		// Called from the runtime
		internal void StartCallback ()
    {
			if (m_start is ThreadStart) {
				var del = (ThreadStart)m_start;
				m_start = null;
				del ();
			} else {
				var del = (ParameterizedThreadStart)m_start;
				var arg = m_start_arg;
				m_start = null;
				m_start_arg = null;
				del (arg);
			}
		}

		public static bool Yield ()
		{
			return YieldInternal ();
		}

		public bool TrySetApartmentStateUnchecked (ApartmentState state) => false;

		static ThreadState ValidateThreadState ()
    {
			var state = GetState (this);
			if ((state & ThreadState.Stopped) != 0)
				throw new ThreadStateException ("Thread is dead; state can not be accessed.");
			return state;
		}

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static void InitInternal (Thread thread);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static Thread GetCurrentThread ();

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern void Thread_free_internal();

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static ThreadState GetState (Thread thread);

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static void SetState (Thread thread, ThreadState set);

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static void ClrState (Thread thread, ThreadState clr);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static string GetName (Thread thread);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static void SetName (Thread thread, String name);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static bool YieldInternal ();

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static void SleepInternal (int millisecondsTimeout);

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static void SpinWait_nop ();

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static Thread CreateInternal ();

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		extern static void StartInternal (Thread runtime_thread);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static bool JoinInternal (Thread thread, int millisecondsTimeout);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static void InterruptInternal (Thread thread);

		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static void SetPriority (Thread thread, int priority);
	}
}
