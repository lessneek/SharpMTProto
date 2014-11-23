# SharpMTProto

**WARNING: the library is not implemented yet (consider donation to speed up development process).**

C# [MTProto Mobile Protocol](http://core.telegram.org/mtproto) implementation.

## Usage

To install SharpMTProto via NuGet, run the following command in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console):

```powershell
PM> Install-Package SharpMTProto
```

## Supported platforms:

- .NET Framework 4.5
- .NET for Windows Store apps
- .NET for Windows Phone 8 apps
- Portable Class Libraries

## Change log

#### SharpMTProto v0.7

- Added 'Client' prefix for all client's things.
- `SocketExtensions`: added `AcceptAsync` ext-method.
- `TcpClientTransport`: added support for server side.
- Build scripts: removed `psake`, added: `gitversion`, added `appveyor.yml` build script.

#### SharpMTProto v0.6

- `MTProtoConnection`: added MTProto `Methods` property.
- `RpcResultHandler`: added support for `IRpcError`.
- Fixed bug with incorrect container `seqno` checking.
- Added `KeyChain` property to `AuthKeyNegotiator`.
- `MessageSendingFlags`: added `RPC` value.
- `TcpTransport`: added a disposable bits.
- `MTProtoConnection`: `Dispose()` method now calls `Dispose()` of an underlying transport.

#### SharpMTProto v0.5.1

- Changed target framework .NET 4.5 <- 4.5.1.

#### SharpMTProto v0.5

- Added `MTProtoBuilder`. It allows easily building of `IMTProtoConnection` and `IAuthKeyNegotiator`.
- Renamed `TransportConfig` to `ITransportConfig`.
- Extracted `IAuthKeyNegotiator` from `AuthKeyNegotiator`.
- Added `HashServices` for PCL.

#### SharpMTProto v0.4.3

- Updated ref to SharpTL v0.7.2.
- Update psake lib for a build script.
- Added test for `MTProtoConnectionFactory`.

#### SharpMTProto v0.4.2

- Updated NuGet package dependencies to correspond project dependencies.

#### SharpMTProto v0.4.1

- Fix `MessageSerializer` bug. The problem was in wrong `TLStreamer` position retained after writing of the body length before the body, hence position left on a serialized message body beginning, but must be at the end.
- Fix `Message.Equals()` bug with bad `Body` comparing.
- Replace dependency type with interface. `FirstRequestResponseHandler`: replace dependency type (`RequestsManager`) with interface (`IRequestsManager`).
- Various additions in a tests project to cover a new code after last huge refactoring.

#### SharpMTProto v0.4

- Updated `BadMsgNotificationHandler` (refactoring). Updated debug message and added throwing of `NotImplementedException` on every unhandled error code.
- Updated completed task returning from Nito.AsyncEx.
- Updated dependent packages.
- Implemented `RPC` in `MTProtoConnection`.
- Huge refactoring (thanks to [Frank Ebersoll](https://github.com/frankebersoll) for ideas).
- Renamed `MessageProcessor` to `MessageCodec`.
- Renamed `MessageCodec` methods: `Wrap()`/`Unwrap()` to `Encode()`/`Decode()`.
- Added `RandomGenerator`.
- Updated build script.
- Added `SendEncryptedMessage<TResponse>()` mthod to the `IMTProtoConnection`.
- `MTProtoConnection`: `SendMessage`'s `TResponse` now can be value type too.

#### SharpMTProto v0.3.1

- Added computation of an initial salt in `AuthKeyNegotiator`.
- Added serializers preparation into `MTProtoConnection`.

#### SharpMTProto v0.3

- Change log messages format for hex strings.
- Added `AuthKeyNegotiator` as separate class (moved from `MTProtoClient`).
- Added sending/receiving encrypted messages to the `MTProtoConnection` class.
- Added checking of `MsgKey` in the `EcryptedMessage` class.
- Renamed `UnencryptedMessage` to `PlainMessage`.
- Fixed message ID generator and unix time utils.
- Added EncryptedMessage class.
- Fixed bytes alignment in MTProtoClient.
- Updated to SharpTL 0.5.
- Updated build scripts.

#### SharpMTProto v0.2.1

- Implemented correct RSA encryption.
- Added a couple of checks and default timeouts to the `MTProtoConnection`.
- Fixed transport bugs.
- Improved build scripts.

#### SharpMTProto v0.1

- SharpTL referenced as NuGet package. Submodule removed.
-Implemented the `KeyChain`'s compute key fingerprint logic.
- Added timeout and cancellation logic to the MTProtoClient and MTProtoConnection.
- Added MTProtoConnectionFactory.
- Implemented creation of auth key logic.
- Implemented in/out logic of the MTProtoConnection with RX. Changed MTProtoSchema names of items to PascalCase.
Working on creating auth key logic.

## Donation ###

**Bitcoin** address: [1FgVQvPmaZ1cdM8TqaH5UPuwp7CPTiMm4h](bitcoin:1FgVQvPmaZ1cdM8TqaH5UPuwp7CPTiMm4h?label=SharpMTProto&message=123)
