// HA DeskLink - Home Assistant Companion App
// Copyright (C) 2026 Fabian Kirchweger
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License v3 as published by
// the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Noise;

namespace HaDeskLink.Sendspin;

/// <summary>
/// Noise KKpsk2 handshake for the Sendspin protocol — client (responder) side.
/// The SERVER is the Noise initiator (it sends message 1); the client waits,
/// reads the psk_id from message 1's payload, mixes the matching PSK, and
/// answers with message 2. Mirrors aiosendspin/noise/{driver,session,keys}.py.
/// </summary>
public static class NoiseHandshake
{
    public const int ProtocolVersion = 1;
    public const string SuiteChaChaPoly = "25519_ChaChaPoly_SHA256";

    // Published constant PSK used when no other PSK applies (unpaired access).
    public static readonly byte[] SentinelPsk = Sha256Utf8("sendspin-sentinel-psk-v1");

    private static readonly byte[] PskIdLabel = Encoding.UTF8.GetBytes("sendspin-psk-id-v1");
    private static readonly byte[] PlaceholderPsk = new byte[32];

    // Transport-mode plaintext type bytes (first byte of the Noise AEAD plaintext).
    public const byte MsgTypeJsonBody = 0;
    public const byte MsgTypeFragmentMore = 2;
    public const byte MsgTypeFragmentEnd = 3;

    // Noise's 65535-byte transport message limit minus the 16-byte AEAD tag.
    public const int MaxTransportPlaintext = 65535 - 16;

    /// <summary>Outcome of a successful Noise handshake.</summary>
    public sealed class HandshakeResult
    {
        /// <summary>Transport-mode encrypt/decrypt pair (Noise.NET Transport).</summary>
        public required Transport Transport { get; init; }
        /// <summary>The server's static public key, base64url (43 chars).</summary>
        public required string ServerId { get; init; }
        /// <summary>The 32-byte Noise handshake hash h.</summary>
        public required byte[] HandshakeHash { get; init; }
        /// <summary>True when the Sentinel PSK admitted the connection (unpaired).</summary>
        public required bool UsedSentinelPsk { get; init; }
    }

    /// <summary>Thrown when the Noise handshake cannot complete.</summary>
    public sealed class HandshakeAbortedException : Exception
    {
        public HandshakeAbortedException(string message) : base(message) { }
    }

    /// <summary>
    /// Generate a fresh static X25519 identity (private + public key).
    /// Persist both via <see cref="SendspinIdentityStore"/>; the public key is
    /// the Sendspin client_id.
    /// </summary>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateIdentity()
    {
        using var kp = KeyPair.Generate();
        return ((byte[])kp.PrivateKey.Clone(), (byte[])kp.PublicKey.Clone());
    }

    /// <summary>SHA-256 over the UTF-8 bytes of <paramref name="s"/>.</summary>
    private static byte[] Sha256Utf8(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));

    /// <summary>base64url without padding.</summary>
    public static string B64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>base64url decode, tolerating missing padding.</summary>
    public static byte[] B64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    /// <summary>psk_id = base64url(SHA-256("sendspin-psk-id-v1" || PSK)).</summary>
    public static string PskIdFor(byte[] psk)
    {
        if (psk.Length != 32)
            throw new ArgumentException("PSK must be 32 bytes");
        var buf = new byte[PskIdLabel.Length + psk.Length];
        PskIdLabel.CopyTo(buf, 0);
        psk.CopyTo(buf, PskIdLabel.Length);
        return B64UrlEncode(SHA256.HashData(buf));
    }

    /// <summary>The published Sentinel psk_id (constant).</summary>
    public static string SentinelPskId => PskIdFor(SentinelPsk);

    /// <summary>
    /// Run the full cleartext init exchange + KKpsk2 handshake as the client
    /// (Noise responder). <paramref name="sendText"/>/<paramref name="receiveText"/>
    /// exchange WebSocket TEXT frames; Noise messages travel base64url-encoded
    /// inside {"type":"noise/handshake","payload":{"data":"..."}} JSON envelopes.
    /// </summary>
    /// <param name="localStaticPriv">32-byte local X25519 static private key.</param>
    /// <param name="localStaticPub">32-byte local X25519 static public key (client_id).</param>
    /// <param name="sendText">Sends one WebSocket text frame.</param>
    /// <param name="receiveText">Receives one WebSocket text frame.</param>
    /// <param name="expectedServerId">Optional pinned server_id (base64url, 43 chars).</param>
    /// <param name="longTermPsk">Optional long-term PSK; the Sentinel PSK is the fallback.</param>
    public static async Task<HandshakeResult> RunClientAsync(
        byte[] localStaticPriv,
        byte[] localStaticPub,
        Func<string, Task> sendText,
        Func<Task<string>> receiveText,
        string? expectedServerId = null,
        byte[]? longTermPsk = null)
    {
        var protocol = CreateProtocol();
        var clientId = B64UrlEncode(localStaticPub);

        // 1. client/init (cleartext JSON text frame)
        var clientInitText = JsonSerializer.Serialize(new
        {
            payload = new { client_id = clientId, version = ProtocolVersion, suite = SuiteChaChaPoly },
            type = "client/init"
        });
        await sendText(clientInitText);

        // 2. server/init (cleartext JSON text frame)
        var serverInitText = await receiveText();
        string serverId;
        using (var doc = JsonDocument.Parse(serverInitText))
        {
            var root = doc.RootElement;
            if (root.GetProperty("type").GetString() != "server/init")
                throw new HandshakeAbortedException("expected server/init");
            if (root.GetProperty("payload").GetProperty("version").GetInt32() != ProtocolVersion)
                throw new HandshakeAbortedException("unsupported protocol version");
            serverId = root.GetProperty("payload").GetProperty("server_id").GetString()
                ?? throw new HandshakeAbortedException("server/init missing server_id");
        }
        if (expectedServerId is not null && serverId != expectedServerId)
            throw new HandshakeAbortedException(
                $"server_id mismatch: expected {expectedServerId}, got {serverId}");

        var serverStaticPub = B64UrlDecode(serverId);
        if (serverStaticPub.Length != 32)
            throw new HandshakeAbortedException("invalid server_id length");

        // Prologue = exact bytes of client/init followed by server/init.
        var prologue = Encoding.UTF8.GetBytes(clientInitText + serverInitText);

        // 3. Noise message 1 (server is the initiator). First pass with the
        // all-zero placeholder PSK: KKpsk2's message 1 is decryptable without
        // the PSK, so we can read the psk_id it carries.
        var hs1Text = await receiveText();
        var msg1Ciphertext = ReadHandshakeEnvelope(hs1Text);

        string pskId;
        using (var probe = CreateProtocol().Create(initiator: false, prologue: prologue,
            s: localStaticPriv, rs: serverStaticPub, psks: new[] { PlaceholderPsk }))
        {
            var ptBuffer = new byte[MaxTransportPlaintext];
            var (ptLen, _, _) = probe.ReadMessage(msg1Ciphertext, ptBuffer);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(ptBuffer, 0, ptLen));
            pskId = doc.RootElement.GetProperty("psk_id").GetString()
                ?? throw new HandshakeAbortedException("Noise message 1 missing psk_id");
        }

        // Resolve the PSK: a stored long-term record, or the Sentinel fallback.
        byte[] psk;
        bool usedSentinel;
        if (longTermPsk is not null && PskIdFor(longTermPsk) == pskId)
        {
            psk = longTermPsk;
            usedSentinel = false;
        }
        else if (pskId == SentinelPskId)
        {
            psk = SentinelPsk;
            usedSentinel = true;
        }
        else
        {
            throw new HandshakeAbortedException($"no PSK matches psk_id={pskId}");
        }

        // Second pass with the real PSK: replay message 1 (identical state up to
        // the psk2 mixing point — Python's noise library swaps the PSK between
        // reads via set_psks(); Noise.NET consumes psks positionally when the
        // pattern reaches the psk2 token, which happens in message 2, so both
        // approaches produce the same chain keys for message 2).
        using var state = CreateProtocol().Create(initiator: false, prologue: prologue,
            s: localStaticPriv, rs: serverStaticPub, psks: new[] { psk });
        state.ReadMessage(msg1Ciphertext, new byte[MaxTransportPlaintext]);

        // 4. Noise message 2: client responds with the empty JSON object "{}".
        var msg2Buffer = new byte[MaxTransportPlaintext + 128];
        var (msg2Written, handshakeHash, transport) =
            state.WriteMessage("{}"u8, msg2Buffer);
        if (transport is null || handshakeHash is null)
            throw new HandshakeAbortedException("handshake did not complete after message 2");

        var msg2Ciphertext = new byte[msg2Written];
        Array.Copy(msg2Buffer, msg2Ciphertext, msg2Written);
        await sendText(PackHandshakeEnvelope(msg2Ciphertext));

        return new HandshakeResult
        {
            Transport = transport,
            ServerId = serverId,
            HandshakeHash = handshakeHash,
            UsedSentinelPsk = usedSentinel,
        };
    }

    /// <summary>
    /// Server-initiated re-handshake in transport mode (Sentinel → long-term PSK
    /// promotion after pairing). The prologue is the prior handshake hash h.
    /// Mirrors run_rehandshake_client(). Reserved for a later pairing phase.
    /// </summary>
    public static HandshakeResult RunRehandshakeClient(
        byte[] localStaticPriv,
        string serverId,
        byte[] priorHandshakeHash,
        byte[]? longTermPsk,
        string hs1EnvelopeText,
        out byte[] msg2CiphertextOut)
    {
        var serverStaticPub = B64UrlDecode(serverId);
        var msg1Ciphertext = ReadHandshakeEnvelope(hs1EnvelopeText);

        string pskId;
        using (var probe = CreateProtocol().Create(initiator: false, prologue: priorHandshakeHash,
            s: localStaticPriv, rs: serverStaticPub, psks: new[] { PlaceholderPsk }))
        {
            var ptBuffer = new byte[MaxTransportPlaintext];
            var (ptLen, _, _) = probe.ReadMessage(msg1Ciphertext, ptBuffer);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(ptBuffer, 0, ptLen));
            pskId = doc.RootElement.GetProperty("psk_id").GetString() ?? "";
        }

        byte[] psk;
        bool usedSentinel;
        if (longTermPsk is not null && PskIdFor(longTermPsk) == pskId)
        {
            psk = longTermPsk;
            usedSentinel = false;
        }
        else if (pskId == SentinelPskId)
        {
            psk = SentinelPsk;
            usedSentinel = true;
        }
        else
        {
            throw new HandshakeAbortedException($"no PSK matches psk_id={pskId}");
        }

        using var state = CreateProtocol().Create(initiator: false, prologue: priorHandshakeHash,
            s: localStaticPriv, rs: serverStaticPub, psks: new[] { psk });
        state.ReadMessage(msg1Ciphertext, new byte[MaxTransportPlaintext]);

        var msg2Buffer = new byte[MaxTransportPlaintext + 128];
        var (msg2Written, handshakeHash, transport) = state.WriteMessage("{}"u8, msg2Buffer);
        if (transport is null || handshakeHash is null)
            throw new HandshakeAbortedException("re-handshake did not complete after message 2");

        msg2CiphertextOut = new byte[msg2Written];
        Array.Copy(msg2Buffer, msg2CiphertextOut, msg2Written);

        return new HandshakeResult
        {
            Transport = transport,
            ServerId = serverId,
            HandshakeHash = handshakeHash,
            UsedSentinelPsk = usedSentinel,
        };
    }

    /// <summary>Build the Noise_KKpsk2_25519_ChaChaPoly_SHA256 protocol.</summary>
    public static Protocol CreateProtocol() => new(
        HandshakePattern.KK,
        CipherFunction.ChaChaPoly,
        HashFunction.Sha256,
        PatternModifiers.Psk2);

    // ─── Envelope helpers ────────────────────────────────────────────────

    internal static byte[] ReadHandshakeEnvelope(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.GetProperty("type").GetString() != "noise/handshake")
            throw new HandshakeAbortedException("expected noise/handshake");
        var data = root.GetProperty("payload").GetProperty("data").GetString()
            ?? throw new HandshakeAbortedException("noise/handshake missing data");
        return B64UrlDecode(data);
    }

    internal static string PackHandshakeEnvelope(byte[] noiseBytes) => JsonSerializer.Serialize(new
    {
        payload = new { data = B64UrlEncode(noiseBytes) },
        type = "noise/handshake"
    });
}

/// <summary>
/// Persists the Sendspin static identity (X25519 private + public key) under
/// the app config directory, DPAPI-encrypted (user scope). The public key is
/// the client_id the server uses to identify us across reconnects.
/// </summary>
public static class SendspinIdentityStore
{
    private static string StoreDir => Path.Combine(Config.GetConfigDir(), "sendspin");
    private static string PrivPath => Path.Combine(StoreDir, "identity.bin");

    /// <summary>Load the persisted identity or generate + persist a fresh one.</summary>
    public static (byte[] PrivateKey, byte[] PublicKey, string ClientId) LoadOrGenerate()
    {
        Directory.CreateDirectory(StoreDir);
        try
        {
            if (File.Exists(PrivPath))
            {
                var cipher = File.ReadAllBytes(PrivPath);
                var plain = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
                if (plain.Length == 64)
                {
                    var priv = new byte[32];
                    var pub = new byte[32];
                    Array.Copy(plain, 0, priv, 0, 32);
                    Array.Copy(plain, 32, pub, 0, 32);
                    return (priv, pub, NoiseHandshake.B64UrlEncode(pub));
                }
            }
        }
        catch
        {
            // corrupt/inaccessible — regenerate below
        }

        var (privateKey, publicKey) = NoiseHandshake.GenerateIdentity();
        var blob = new byte[64];
        Array.Copy(privateKey, 0, blob, 0, 32);
        Array.Copy(publicKey, 0, blob, 32, 32);
        File.WriteAllBytes(PrivPath,
            ProtectedData.Protect(blob, null, DataProtectionScope.CurrentUser));
        return (privateKey, publicKey, NoiseHandshake.B64UrlEncode(publicKey));
    }
}