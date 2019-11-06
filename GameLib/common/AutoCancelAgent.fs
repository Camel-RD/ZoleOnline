namespace GameLib

open System
open System.Threading

type Agent<'T> = MailboxProcessor<'T>

type AutoCancelAgent<'T> (f) = 
  let cts = new CancellationTokenSource()
  let mbox = new Agent<'T>(f, cancellationToken = cts.Token)
  
  member x.Start() = mbox.Start()
  
  member x.CurrentQueueLength = mbox.CurrentQueueLength

  [<CLIEvent>]
  member x.Error = mbox.Error

  member x.Receive(?timeout) = mbox.Receive(?timeout = timeout)

  member x.Scan(scanner, ?timeout) = mbox.Scan(scanner, ?timeout = timeout)

  member x.TryPostAndReply(buildMessage, ?timeout) = 
    mbox.TryPostAndReply(buildMessage, ?timeout = timeout)

  member x.TryReceive(?timeout) = 
    mbox.TryReceive(?timeout = timeout)

  member x.TryScan(scanner, ?timeout) = 
    mbox.TryScan(scanner, ?timeout = timeout)

  member x.Post(m) = mbox.Post(m)

  member x.PostAndReply(buildMessage, ?timeout) = 
    mbox.PostAndReply(buildMessage, ?timeout = timeout)

  member x.PostAndTryAsyncReply(buildMessage, ?timeout) = 
    mbox.PostAndTryAsyncReply(buildMessage, ?timeout = timeout)

  member x.PostAndAsyncReply(buildMessage, ?timeout) = 
    mbox.PostAndAsyncReply(buildMessage, ?timeout=timeout)

  interface IDisposable with
    member x.Dispose() = 
      (mbox :> IDisposable).Dispose()
      cts.Cancel()