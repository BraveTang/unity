using System;
using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace PubNubMessaging.Core
{
	#region EventExt and Args
	static class EventExtensions
	{
		public static void Raise<T> (this EventHandler<T> handler, object sender, T args)
            where T : EventArgs
		{
			if (handler != null) {
				handler (sender, args);
			}
		}
	}

	internal class CustomEventArgs<T> : EventArgs
	{
		internal string Message;
		internal RequestState<T> PubnubRequestState;
		internal bool IsError;
		internal bool IsTimeout;
		internal CurrentRequestType CurrRequestType;
	}
	#endregion

	#region CoroutineClass
	/*internal class ForceQuitCoroutieArgs<T> : EventArgs
    {
        internal bool isTimeout;
        internal IEnumerator crTimeout;
        internal IEnumerator crRequest;
    }*/

	/*class ProcessRequests<T> {
        public event EventHandler<EventArgs> CoroutineComplete;
        public event EventHandler<EventArgs> ForceStopCoroutine;
        public bool isComplete = false;

        public string url;
        public WWW www;
        public RequestState<T> pubnubRequestState;
        public int timeout;
        public IEnumerator crTimeout;
        public IEnumerator crRequest;

        public ProcessRequests(string url, RequestState<T> pubnubRequestState, int timeout){
            this.url = url;
            this.pubnubRequestState = pubnubRequestState;
            this.timeout = timeout;
        }

        public void FireForceStopCoRoutine(bool isTimeout){
            if (ForceStopCoroutine != null) {
                ForceQuitCoroutieArgs<T> fqca = new ForceQuitCoroutieArgs<T> ();
                fqca.isTimeout = isTimeout;
                fqca.crRequest = crRequest;
                fqca.crTimeout = crTimeout;
                ForceStopCoroutine.Raise (this, fqca);
            }
        }

        public IEnumerator SendRequest<T> ()
        {
            Debug.Log ("URL:" + url.ToString ());
            isComplete = false;
            www = new WWW (url);
            yield return www;

            try {
                if(www != null){
                    FireForceStopCoRoutine (false);

                    if (www.error == null) {
                        isComplete = true;
                        UnityEngine.Debug.Log ("Message: " + www.text);
                        FireEvent (www.text, false, false, pubnubRequestState);
                    } else {
                        isComplete = true;
                        UnityEngine.Debug.Log ("Error: " + www.error);
                        FireEvent (www.error, true, false, pubnubRequestState);
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Error: {1}", DateTime.Now.ToString (), www.error), LoggingMethod.LevelError);
                    } 
                } 
            } catch (Exception ex) {
                LoggingMethod.WriteToLog (string.Format ("DateTime {0}, RunCoroutine {1}", DateTime.Now.ToString (), ex.ToString ()), LoggingMethod.LevelError);
            }
            LoggingMethod.WriteToLog (string.Format ("DateTime {0}, SendRequest exit", DateTime.Now.ToString ()), LoggingMethod.LevelError);
        }

        public IEnumerator CheckTimeout<T> ()
        {
            LoggingMethod.WriteToLog (string.Format ("DateTime {0}, yielding: {1} sec timeout", DateTime.Now.ToString (), timeout.ToString ()), LoggingMethod.LevelError);
            yield return new WaitForSeconds(timeout); 
            if (!isComplete) {

                if (www != null) {
                    www.Dispose ();
                }

                FireEvent ("Timed out", true, true, pubnubRequestState);

                LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Error: {1} sec timeout", DateTime.Now.ToString (), timeout.ToString ()), LoggingMethod.LevelError);

                FireForceStopCoRoutine (true);
            }
            LoggingMethod.WriteToLog (string.Format ("DateTime {0}, CheckTimeout exit", DateTime.Now.ToString ()), LoggingMethod.LevelError);
        }

        public void FireEvent<T> (string message, bool isError, bool isTimeout, RequestState<T> pubnubRequestState)
        {
            if (CoroutineComplete != null) {
                CustomEventArgs<T> cea = new CustomEventArgs<T> ();
                cea.pubnubRequestState = pubnubRequestState;
                cea.message = message;
                cea.isError = isError;
                cea.isTimeout = isTimeout;
                CoroutineComplete.Raise (this, cea);
            }
        }
    }*/

	class CoroutineParams
	{
		public string url;
		public int timeout;
		public int pause;
		public CurrentRequestType crt;
		public Type typeParameterType;

		public CoroutineParams (string url, int timeout, int pause, CurrentRequestType crt, Type typeParameterType)
		{
			this.url = url;
			this.timeout = timeout;
			this.pause = pause;
			this.crt = crt;
			this.typeParameterType = typeParameterType;
		}
	}

	public enum CurrentRequestType
	{
		Heartbeat,
		PresenceHeartbeat,
		Subscribe,
		NonSubscribe
	}

	//TODO: Refactor after stable code, separate out repeat code.
	//Sending a IEnumerator from a complex object in StartCoroutine doesn't work for Web/WebGL
	//Dispose of www leads to random unhandled exceptions.
	//Generic methods dont work in StartCoroutine when the called with the string param name StartCoroutine("method", param)
	//StopCoroutine only works when the coroutine is started with string overload.
	class CoroutineClass : MonoBehaviour
	{
		#region "IL2CPP workarounds"

		//Got an exception when using JSON serialisation for [],
		//IL2CPP needs to know about the array type at compile time.
		//So please define private static filed like this:
		private static System.String[][] _unused;
		private static System.Int32[][] _unused2;
		private static System.Int64[][] _unused3;
		private static System.Int16[][] _unused4;
		private static System.UInt16[][] _unused5;
		private static System.UInt64[][] _unused6;
		private static System.UInt32[][] _unused7;
		private static System.Decimal[][] _unused8;
		private static System.Double[][] _unused9;
		private static System.Boolean[][] _unused91;
		private static System.Object[][] _unused92;
     
		private static long[][] _unused10;
		private static int[][] _unused11;
		private static float[][] _unused12;
		private static decimal[][] _unused13;
		private static uint[][] _unused14;
		private static ulong[][] _unused15;

		#endregion

		private bool isHearbeatComplete = false;
		private bool isPresenceHeartbeatComplete = false;
		private bool isSubscribeComplete = false;
		private bool isNonSubscribeComplete = false;

		WWW subscribeWww;
		WWW heartbeatWww;
		WWW presenceHeartbeatWww;
		WWW nonSubscribeWww;

		private EventHandler<EventArgs> subCoroutineComplete;
		//Register single event handler
		public event EventHandler<EventArgs> SubCoroutineComplete {
			add {
				if (subCoroutineComplete == null || !subCoroutineComplete.GetInvocationList ().Contains (value)) {
					subCoroutineComplete += value;
				}
			}
			remove {
				subCoroutineComplete -= value;
			}
		}

		private EventHandler<EventArgs> nonSubCoroutineComplete;
		//Register single event handler
		public event EventHandler<EventArgs> NonSubCoroutineComplete {
			add {
				if (nonSubCoroutineComplete == null || !nonSubCoroutineComplete.GetInvocationList ().Contains (value)) {
					nonSubCoroutineComplete += value;
				}
			}
			remove {
				nonSubCoroutineComplete -= value;
			}
		}

		private EventHandler<EventArgs> presenceHeartbeatCoroutineComplete;
		//Register single event handler
		public event EventHandler<EventArgs> PresenceHeartbeatCoroutineComplete {
			add {
				if (presenceHeartbeatCoroutineComplete == null || !presenceHeartbeatCoroutineComplete.GetInvocationList ().Contains (value)) {
					presenceHeartbeatCoroutineComplete += value;
				}
			}
			remove {
				presenceHeartbeatCoroutineComplete -= value;
			}
		}

		private EventHandler<EventArgs> heartbeatCoroutineComplete;
		//Register single event handler
		public event EventHandler<EventArgs> HeartbeatCoroutineComplete {
			add {
				if (heartbeatCoroutineComplete == null || !heartbeatCoroutineComplete.GetInvocationList ().Contains (value)) {
					heartbeatCoroutineComplete += value;
				}
			}
			remove {
				heartbeatCoroutineComplete -= value;
			}
		}


		public void Run<T> (string url, RequestState<T> pubnubRequestState, int timeout, int pause)
		{
			#region "Process request "
			/*ProcessRequests<T> pr = new ProcessRequests<T> (url, pubnubRequestState, timeout);
            pr.CoroutineComplete += HandleCoroutineComplete;
            pr.ForceStopCoroutine += HandleForceStopCoroutine<T>;
            pr.crTimeout = pr.CheckTimeout<T> ();
            pr.crRequest = pr.SendRequest<T> ();

            //for heartbeat and presence heartbeat treat reconnect as pause
            if (((pubnubRequestState.Type == ResponseType.Heartbeat) || (pubnubRequestState.Type == ResponseType.PresenceHeartbeat))
                && (pubnubRequestState.Reconnect == true)) {

                StartCoroutine (PausedRequest<T>(pr, pause));
            } else {
                IEnumerator crTimeout = pr.crTimeout;
                IEnumerator crRequest = pr.crRequest;

                StartCoroutine (crTimeout);
                StartCoroutine (crRequest);
            }*/
			#endregion

			//for heartbeat and presence heartbeat treat reconnect as pause
			CurrentRequestType crt;
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, RequestType {1} {2}", DateTime.Now.ToString (), typeof(T), pubnubRequestState.GetType ()), LoggingMethod.LevelInfo);
			if ((pubnubRequestState.Type == ResponseType.Heartbeat) || (pubnubRequestState.Type == ResponseType.PresenceHeartbeat)) {
				crt = CurrentRequestType.PresenceHeartbeat;
				if (pubnubRequestState.Type == ResponseType.Heartbeat) {
					crt = CurrentRequestType.Heartbeat;
				} 
				CheckComplete (crt);

				if (pubnubRequestState.Reconnect) {
					StartCoroutine (DelayRequest<T> (url, pubnubRequestState, timeout, pause, crt));
				} else {
					StartCoroutinesByName<T> (url, pubnubRequestState, timeout, pause, crt);
				}
			} else if ((pubnubRequestState.Type == ResponseType.Subscribe) || (pubnubRequestState.Type == ResponseType.Presence)) {
				crt = CurrentRequestType.Subscribe;
                
				if ((subscribeWww != null) && (!subscribeWww.isDone)) {
					LoggingMethod.WriteToLog (string.Format ("DateTime {0}, subscribeWww running trying to abort {1}", DateTime.Now.ToString (), crt.ToString ()), LoggingMethod.LevelInfo);
					if (subscribeWww == null) {
						LoggingMethod.WriteToLog (string.Format ("DateTime {0}, subscribeWww aborted {1}", DateTime.Now.ToString (), crt.ToString ()), LoggingMethod.LevelInfo);
					}
				}
				StartCoroutinesByName<T> (url, pubnubRequestState, timeout, pause, crt);
			} else {
				crt = CurrentRequestType.NonSubscribe;
				CheckComplete (crt);
				StartCoroutinesByName<T> (url, pubnubRequestState, timeout, pause, crt);
			} 
		}

		private void StartCoroutinesByName<T> (string url, RequestState<T> pubnubRequestState, int timeout, int pause, CurrentRequestType crt)
		{
			CoroutineParams cp = new CoroutineParams (url, timeout, pause, crt, typeof(T));

			if (crt == CurrentRequestType.Subscribe) {
				StartCoroutine ("CheckTimeoutSub", cp);
				StartCoroutine ("SendRequestSub", cp);
			} else if (crt == CurrentRequestType.NonSubscribe) {
				StartCoroutine ("CheckTimeoutNonSub", cp);
				StartCoroutine ("SendRequestNonSub", cp);
			} else if (crt == CurrentRequestType.PresenceHeartbeat) {
				StartCoroutine ("CheckTimeoutPresenceHeartbeat", cp);
				StartCoroutine ("SendRequestPresenceHeartbeat", cp);
			} else if (crt == CurrentRequestType.Heartbeat) {
				StartCoroutine ("CheckTimeoutHeartbeat", cp);
				StartCoroutine ("SendRequestHeartbeat", cp);
			}
		}

		public IEnumerator DelayRequest<T> (string url, RequestState<T> pubnubRequestState, int timeout, int pause, CurrentRequestType crt)
		{
			yield return new WaitForSeconds (pause); 
			StartCoroutinesByName<T> (url, pubnubRequestState, timeout, pause, crt);

			/*IEnumerator crTimeout = pr.crTimeout;
            IEnumerator crRequest = pr.crRequest;

            StartCoroutine (crTimeout);
            StartCoroutine (crRequest);*/
		}

		public void ProcessResponse (WWW www, CoroutineParams cp)
		{
			try {
				if (www != null) {
					LoggingMethod.WriteToLog (string.Format ("DateTime {0}, Before set complete sub {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
					SetComplete (cp.crt);
					LoggingMethod.WriteToLog (string.Format ("DateTime {0}, After set complete sub {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
					string message = "";
					bool isError = false;

					if (string.IsNullOrEmpty (www.error)) {
						LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Sub {1} Message: {2}", DateTime.Now.ToString (), cp.crt.ToString (), www.text), LoggingMethod.LevelInfo);
						message = www.text;
						isError = false;
					} else {
						LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Sub {1} Error: {2}", DateTime.Now.ToString (), cp.crt.ToString (), www.error), LoggingMethod.LevelInfo);
						message = www.error;
						isError = true;
					} 
					;
					if (cp.typeParameterType == typeof(string)) {
						var requestState = StoredRequestState.Instance.GetStoredRequestState (cp.crt) as RequestState<string>;
						FireEvent (message, isError, false, requestState, cp.crt);
					} else if (cp.typeParameterType == typeof(object)) {
						var requestState = StoredRequestState.Instance.GetStoredRequestState (cp.crt) as RequestState<object>;
						FireEvent (message, isError, false, requestState, cp.crt);
					} else {
						throw new Exception ("'string' and 'object' are the only types supported in generic method calls.");
					}
				} 
			} catch (Exception ex) {
				LoggingMethod.WriteToLog (string.Format ("DateTime {0}, RunCoroutineSub {1}, Exception: {2}", DateTime.Now.ToString (), cp.crt.ToString (), ex.ToString ()), LoggingMethod.LevelError);
			}
		}

		public IEnumerator SendRequestSub (CoroutineParams cp)
		{
			Debug.Log ("URL Sub:" + cp.url.ToString ());
			WWW www;


			isSubscribeComplete = false;

			subscribeWww = new WWW (cp.url);
			yield return subscribeWww;
			if ((subscribeWww != null) && (subscribeWww.isDone)) {
				www = subscribeWww;
			} else {
				www = null;
				System.GC.Collect ();
			}

			ProcessResponse (www, cp);

			#region "code that breaks on WebGL due to a unity issue"
			/*try {
                if (www != null) {

                    SetComplete (cp.crt);
                    LoggingMethod.WriteToLog (string.Format ("DateTime {0}, After set complete sub {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelError);
                    string message = "";
                    bool isError = false;

                    if (string.IsNullOrEmpty (www.error)) {
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Sub {1} Message: {2}", DateTime.Now.ToString (), cp.crt.ToString (), www.text), LoggingMethod.LevelError);

                        message = www.text;
                        isError = false;
                    } else {
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Sub {1} Error: {2}", DateTime.Now.ToString (), cp.crt.ToString (), www.error), LoggingMethod.LevelError);
                        message = www.error;
                        isError = true;
                    }*/ 
                    
			#region "working code"
			/*Type generic = typeof(StoredRequestState);
                        Type constructed = generic.MakeGenericType (typeArgs);
                        var repository = Activator.CreateInstance (generic);
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository Activator.CreateInstance", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                        if(repository == null){
                            LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                        }
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository Invoking method", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                        MethodInfo repositorymethod = repository.GetType().GetMethod("GetStoredRequestState").MakeGenericMethod(typeArgs);
                        var a = repositorymethod.Invoke (repository, new object[] { cp.crt });
                        if(repositorymethod == null){
                            LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repositorymethod null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                        }
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository After invoke", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    */
			#endregion

			#region "working code2"
			/*LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository Invoking method", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    MethodInfo repositorymethod = StoredRequestState.Instance.GetType().GetMethod("GetStoredRequestState").MakeGenericMethod(typeArgs);

                    var a = repositorymethod.Invoke (StoredRequestState.Instance, new object[] { cp.crt });
                    if(repositorymethod == null){
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repositorymethod null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    }
                    LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository After invoke", DateTime.Now.ToString ()), LoggingMethod.LevelError);

                    if(a == null){
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, a null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    } else {
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, a not null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    }*/
			#endregion

			/*Type[] typeArgs = { cp.typeParameterType };
                    Type generic = typeof(RequestState<>);
                    Type constructed = generic.MakeGenericType (typeArgs);

                    var b = StoredRequestState.Instance.GetStoredRequestState(cp.crt);

                    #region "Uncomment when the webgl generic bug is resolved"
                    //var requestState = Convert.ChangeType(b, constructed);
                    #endregion

                    //workaround till webgl generic issue is fixed
                    var requestState = Convert.ChangeType(b, constructed) as RequestState<string>;

                    LoggingMethod.WriteToLog (string.Format ("DateTime {0}, repository change type", DateTime.Now.ToString ()), LoggingMethod.LevelError);

                    if(requestState == null){
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, a null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    } else {
                        LoggingMethod.WriteToLog (string.Format ("DateTime {0}, a not null", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                    }*/
                    
			#region "Uncomment when the webgl generic bug is resolved"
			/*LoggingMethod.WriteToLog (string.Format ("DateTime {0}, CallFireEvent MakeGenericMethod", DateTime.Now.ToString ()), LoggingMethod.LevelError);
                        MethodInfo method = GetType ().GetMethod ("CallFireEvent").MakeGenericMethod(typeArgs);
                        method.Invoke (this, new object[] { message, isError, false, requestState, cp });
                    LoggingMethod.WriteToLog (string.Format ("DateTime {0}, CallFireEvent After invoke", DateTime.Now.ToString ()), LoggingMethod.LevelError);*/
			#endregion

			/*FireEvent(message, isError, false, requestState, cp.crt);

                } 
            } catch (Exception ex) {
                LoggingMethod.WriteToLog (string.Format ("DateTime {0}, RunCoroutineSub {1}, Exception: {2}", DateTime.Now.ToString (), cp.crt.ToString (), ex.ToString ()), LoggingMethod.LevelError);
            }*/
			#endregion

			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, SendRequestSub exit {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
		}

		public IEnumerator SendRequestNonSub (CoroutineParams cp)
		{
			Debug.Log ("URL NonSub:" + cp.url.ToString ());
			WWW www;

			isNonSubscribeComplete = false;
			nonSubscribeWww = new WWW (cp.url);
			yield return nonSubscribeWww;
			if ((nonSubscribeWww != null) && (nonSubscribeWww.isDone)) {
				www = nonSubscribeWww;
			} else {
				www = null;
				System.GC.Collect ();
			}
             
			ProcessResponse (www, cp);
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, SendRequestNonSub exit {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
		}

		public IEnumerator SendRequestPresenceHeartbeat (CoroutineParams cp)
		{
			Debug.Log ("URL PresenceHB:" + cp.url.ToString ());
			WWW www;

			isPresenceHeartbeatComplete = false;
			presenceHeartbeatWww = new WWW (cp.url);
			yield return presenceHeartbeatWww;
			if ((presenceHeartbeatWww != null) && (presenceHeartbeatWww.isDone)) {
				www = presenceHeartbeatWww;
			} else {
				www = null;
				System.GC.Collect ();
			}

			ProcessResponse (www, cp);

			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, SendRequestPresenceHeartbeat exit {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
		}

		public IEnumerator SendRequestHeartbeat (CoroutineParams cp)
		{
			Debug.Log ("URL Heartbeat:" + cp.url.ToString ());
			WWW www;

			isHearbeatComplete = false;
			heartbeatWww = new WWW (cp.url);
			yield return heartbeatWww;
			if ((heartbeatWww != null) && (heartbeatWww.isDone)) {
				www = heartbeatWww;
			} else {
				www = null;
				System.GC.Collect ();
			}

			ProcessResponse (www, cp);
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, SendRequestHeartbeat exit {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
		}

		public void CallFireEvent<T> (string message, bool isError, bool isTimeout, RequestState<T> pubnubRequestState, CoroutineParams cp)
		{
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, CallFireEvent RequestType {1} {2} {3}", DateTime.Now.ToString (), typeof(T), pubnubRequestState.GetType (), pubnubRequestState.Channels), LoggingMethod.LevelInfo);
			FireEvent (message, isError, false, pubnubRequestState, cp.crt);
		}

		void SetComplete (CurrentRequestType crt)
		{
			try {
				if (crt == CurrentRequestType.Heartbeat) {
					isHearbeatComplete = true;
					StopCoroutine ("CheckTimeoutHeartbeat");  
				} else if (crt == CurrentRequestType.PresenceHeartbeat) {
					isPresenceHeartbeatComplete = true;
					StopCoroutine ("CheckTimeoutPresenceHeartbeat");
				} else if (crt == CurrentRequestType.Subscribe) {
					isSubscribeComplete = true;
					StopCoroutine ("CheckTimeoutSub");
				} else {
					isNonSubscribeComplete = true;
					StopCoroutine ("CheckTimeoutNonSub");
				} 
			} catch (Exception ex) {
				LoggingMethod.WriteToLog (string.Format ("DateTime {0}, SetComplete Exception: ", DateTime.Now.ToString (), ex.ToString ()), LoggingMethod.LevelError);
			}

		}

		public bool CheckComplete (CurrentRequestType crt)
		{
			try {
				if (crt == CurrentRequestType.Heartbeat) {
					if ((!isHearbeatComplete) && (heartbeatWww != null) && (!heartbeatWww.isDone)) {    
						StopCoroutine ("SendRequestHeartbeat");
						return false;
					}
				} else if (crt == CurrentRequestType.PresenceHeartbeat) {
					if ((!isPresenceHeartbeatComplete) && (presenceHeartbeatWww != null) && (!presenceHeartbeatWww.isDone)) {
						StopCoroutine ("SendRequestPresenceHeartbeat");
						return false;
					}
				} else if (crt == CurrentRequestType.Subscribe) {
					if ((!isSubscribeComplete) && (subscribeWww != null) && (!subscribeWww.isDone)) {
						StopCoroutine ("SendRequestSub");
						return false;
					}
				} else {
					if ((!isNonSubscribeComplete) && (nonSubscribeWww != null) && (!nonSubscribeWww.isDone)) {
						StopCoroutine ("SendRequestNonSub");
						return false;
					}
				} 

			} catch (Exception ex) {
				LoggingMethod.WriteToLog (string.Format ("DateTime {0}, GetCompleteAndDispose Exception: ", DateTime.Now.ToString (), ex.ToString ()), LoggingMethod.LevelError);
			}

			return true;
		}

		public void BounceRequest<T> (CurrentRequestType crt, RequestState<T> pubnubRequestState, bool fireEvent)
		{
			try {
				if ((crt == CurrentRequestType.Heartbeat) && (heartbeatWww != null) && (!heartbeatWww.isDone)) {
					heartbeatWww.Dispose ();
					heartbeatWww = null;
					StopCoroutine ("SendRequestHeartbeat");
					SetComplete (CurrentRequestType.Heartbeat);
				} else if ((crt == CurrentRequestType.PresenceHeartbeat) && (presenceHeartbeatWww != null) && (!presenceHeartbeatWww.isDone)) {
					presenceHeartbeatWww.Dispose ();
					presenceHeartbeatWww = null;
					StopCoroutine ("SendRequestPresenceHeartbeat");
					SetComplete (CurrentRequestType.PresenceHeartbeat);
				} else if ((crt == CurrentRequestType.Subscribe) && (subscribeWww != null) && (!subscribeWww.isDone)) {
					subscribeWww = null;
					StopCoroutine ("SendRequestSub");

					SetComplete (CurrentRequestType.Subscribe);
				} else if ((crt == CurrentRequestType.NonSubscribe) && (nonSubscribeWww != null) && (!nonSubscribeWww.isDone)) {
					LoggingMethod.WriteToLog (string.Format ("DateTime {0}, Dispose nonSubscribeWww: ", DateTime.Now.ToString ()), LoggingMethod.LevelInfo);
					nonSubscribeWww.Dispose ();
                    
					nonSubscribeWww = null;
					StopCoroutine ("SendRequestNonSub");
					SetComplete (CurrentRequestType.NonSubscribe);
				}
				System.GC.Collect ();
				if ((pubnubRequestState != null) && (fireEvent)) {
					FireEvent ("Aborted", true, false, pubnubRequestState, crt);
				}
			} catch (Exception ex) {
				LoggingMethod.WriteToLog (string.Format ("DateTime {0}, BounceRequest Exception: {1}", DateTime.Now.ToString (), ex.ToString ()), LoggingMethod.LevelError);
			}
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, BounceRequest {1}", DateTime.Now.ToString (), crt.ToString ()), LoggingMethod.LevelInfo);
		}

		public void ProcessTimeout (CoroutineParams cp)
		{
			try {
				if (!CheckComplete (cp.crt)) {
					if (cp.typeParameterType == typeof(string)) {
						var requestState = StoredRequestState.Instance.GetStoredRequestState (cp.crt) as RequestState<string>;
						FireEvent ("Timed out", true, true, requestState, cp.crt);
						LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Error: {1} sec timeout", DateTime.Now.ToString (), cp.timeout.ToString ()), LoggingMethod.LevelInfo);
					} else if (cp.typeParameterType == typeof(object)) { 
						var requestState = StoredRequestState.Instance.GetStoredRequestState (cp.crt) as RequestState<object>;
						FireEvent ("Timed out", true, true, requestState, cp.crt);
						LoggingMethod.WriteToLog (string.Format ("DateTime {0}, WWW Error: {1} sec timeout", DateTime.Now.ToString (), cp.timeout.ToString ()), LoggingMethod.LevelInfo);
					} else {
						throw new Exception ("'string' and 'object' are the only types supported in generic method calls");
					}
				}
				LoggingMethod.WriteToLog (string.Format ("DateTime {0}, CheckTimeout exit {1}", DateTime.Now.ToString (), cp.crt.ToString ()), LoggingMethod.LevelInfo);
			} catch (Exception ex) {
				LoggingMethod.WriteToLog (string.Format ("DateTime {0}, CheckTimeout: {1} {2}", DateTime.Now.ToString (), ex.ToString (), cp.crt.ToString ()), LoggingMethod.LevelError);
			}
		}

		public IEnumerator CheckTimeoutSub (CoroutineParams cp)
		{
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, yielding: {1} sec timeout", DateTime.Now.ToString (), cp.timeout.ToString ()), LoggingMethod.LevelInfo);
			yield return new WaitForSeconds (cp.timeout); 
			ProcessTimeout (cp);
		}

		public IEnumerator CheckTimeoutNonSub (CoroutineParams cp)
		{
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, yielding: {1} sec timeout", DateTime.Now.ToString (), cp.timeout.ToString ()), LoggingMethod.LevelInfo);
			yield return new WaitForSeconds (cp.timeout); 
			ProcessTimeout (cp);
		}

		public IEnumerator CheckTimeoutPresenceHeartbeat (CoroutineParams cp)
		{
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, yielding: {1} sec timeout", DateTime.Now.ToString (), cp.timeout.ToString ()), LoggingMethod.LevelInfo);
			yield return new WaitForSeconds (cp.timeout); 
			ProcessTimeout (cp);
		}

		public IEnumerator CheckTimeoutHeartbeat (CoroutineParams cp)
		{
			LoggingMethod.WriteToLog (string.Format ("DateTime {0}, yielding: {1} sec timeout", DateTime.Now.ToString (), cp.timeout.ToString ()), LoggingMethod.LevelInfo);
			yield return new WaitForSeconds (cp.timeout); 
			ProcessTimeout (cp);
		}

		public void FireEvent<T> (string message, bool isError, bool isTimeout, RequestState<T> pubnubRequestState, CurrentRequestType crt)
		{
			CustomEventArgs<T> cea = new CustomEventArgs<T> ();
			cea.PubnubRequestState = pubnubRequestState;
			cea.Message = message;
			cea.IsError = isError;
			cea.IsTimeout = isTimeout;
			cea.CurrRequestType = crt;
			if ((crt == CurrentRequestType.Heartbeat) && (heartbeatCoroutineComplete != null)) {
				heartbeatCoroutineComplete.Raise (this, cea);
			} else if ((crt == CurrentRequestType.PresenceHeartbeat) && (presenceHeartbeatCoroutineComplete != null)) {
				presenceHeartbeatCoroutineComplete.Raise (this, cea);
			} else if ((crt == CurrentRequestType.Subscribe) && (subCoroutineComplete != null)) {
				subCoroutineComplete.Raise (this, cea);
			} else if ((crt == CurrentRequestType.NonSubscribe) && (nonSubCoroutineComplete != null)) {
				nonSubCoroutineComplete.Raise (this, cea);
			}
		}
	}
	#endregion

	#region "PubnubWebResponse and PubnubWebRequest"
	public class PubnubWebResponse
	{
		WWW www;

		public PubnubWebResponse (WWW www)
		{
			this.www = www;
		}

		public string ResponseUri {
			get {
				return www.url;
			}
		}

		public Dictionary<string, string> Headers {
			get {
				return www.responseHeaders;
			}
		}
	}

	public class PubnubWebRequest
	{
		WWW www;

		public PubnubWebRequest (WWW www)
		{
			this.www = www;
		}

		public string RequestUri {
			get {
				return www.url;
			}
		}

		public Dictionary<string, string> Headers {
			get {
				return www.responseHeaders;
			}
		}

	}
	#endregion
}

