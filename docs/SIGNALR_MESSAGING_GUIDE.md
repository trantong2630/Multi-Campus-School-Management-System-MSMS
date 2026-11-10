# SignalR Hub Guide - Thông báo và Tin nhắn Realtime

## Tổng quan

SignalR Hub (`/signalr`) cung cấp kênh realtime cho:
- **Thông báo** (NotificationCreated)
- **Tin nhắn** (MessageReceived, MessagesRead)
- **Trạng thái gõ tin** (UserTyping)
- **Trạng thái online/offline** (PresenceChanged)

**Luật chính (BR-25)**: Gửi tin vẫn qua REST API và kiểm tra quan hệ. SignalR chỉ là kênh đẩy nhẹ sau khi SaveChanges thành công.

---

## Kết nối Hub

### URL kết nối
```
WebSocket: ws://localhost:5000/signalr?access_token={accessToken}
HTTPS: wss://yourdomain.com/signalr?access_token={accessToken}
```

### Xác thực
- Token được truyền qua query string: `access_token={jwtToken}`
- Nếu token hết hạn hoặc không hợp lệ, kết nối bị từ chối
- Nếu tài khoản bị khóa hoặc phiên bị thu hồi, kết nối bị ngắt

### Tự động vào nhóm
Khi kết nối thành công, client tự động được thêm vào:
```
Nhóm cá nhân: user_{personId}
```

Nhóm hội thoại `conversation_{conversationId}` chỉ có sau khi client gọi `JoinConversation` (xem bên dưới).

School Admin không tham gia hội thoại: không được báo online, không gọi được `SendTyping` và `JoinConversation`.

---

## Sự kiện từ Server → Client

### MessageReceived (Tin nhắn mới)
**Payload**:
```json
{
  "messageId": 123,
  "conversationId": 45,
  "senderPersonId": 10,
  "senderName": "Phạm Văn An",
  "content": "Em chào thầy",
  "sentAt": "2026-10-24T09:15:00Z",
  "readAt": null
}
```

`MessageReceived` được gửi tới cả nhóm hội thoại và nhóm cá nhân của người nhận (để cập nhật bản xem trước và số tin chưa đọc khi họ đang ở hội thoại khác).

### NotificationCreated (Thông báo)
Gửi tới nhóm cá nhân `user_{personId}` của người nhận. Thông báo thường (thời khóa biểu, tiết, bài tập…) được đẩy sau khi lưu thành công, payload:
```json
{
  "notificationId": 88,
  "type": "AssignmentAssigned",
  "title": "Bài tập mới: Ôn tập chương 1",
  "preview": "Môn Toán, hạn nộp 21:00 05/11/2026.",
  "refEntityType": "Assignment",
  "refEntityId": 12,
  "createdAt": "2026-10-24T09:15:00Z"
}
```
Thông báo tin nhắn mới do dịch vụ hộp thư đẩy riêng, payload:
```json
{
  "type": "MessageReceived",
  "title": "Tin nhắn mới từ Phạm Văn An",
  "preview": "Em chào thầy",
  "module": "PAR",
  "targetId": 45,
  "createdAt": "2026-10-24T09:15:00Z"
}
```

### MessagesRead (Tin đã xem)
**Payload**: `[conversationId, readAt]`. Gửi tới nhóm hội thoại khi người nhận mở hội thoại và các tin chưa đọc được đánh dấu đã xem.

### UserTyping (Đang gõ)
**Payload**: `[conversationId, personId, isTyping]`. Chỉ gửi cho bên kia trong nhóm hội thoại, không lưu CSDL; client tự tắt chỉ báo sau vài giây.

### PresenceChanged (Online/offline)
**Payload**: `[personId, isOnline]`. Gửi tới bên kia của mọi hội thoại chung khi tài khoản có kết nối đầu tiên (online) hoặc mất kết nối cuối cùng (offline); nhiều tab hay nhiều thiết bị vẫn tính là online.

---

## Phương thức Client → Server

### SendTyping
```csharp
await connection.invoke("SendTyping", conversationId, isTyping);
```

### JoinConversation
Kiểm người gọi thuộc hội thoại rồi thêm kết nối vào nhóm hội thoại; trả về `true` nếu bên kia đang online.
```csharp
var otherIsOnline = await connection.invoke<bool>("JoinConversation", conversationId);
```

---

## Quy trình Gửi Tin Nhắn

1. Client: `POST /api/par/conversations/{id}/messages`
2. Backend: Kiểm tra quan hệ, tạo Message, SaveChanges
3. Backend: Gửi `MessageReceived` + `NotificationCreated` qua SignalR
4. Client: Nhận sự kiện, cập nhật UI

---

## Web Client (React)

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`/signalr?access_token=${accessToken}`)
  .withAutomaticReconnect()
  .build();

connection.on("MessageReceived", (message) => {
  console.log("New message:", message);
});

connection.on("NotificationCreated", (notification) => {
  console.log("New notification:", notification);
});

await connection.start();
await connection.invoke("JoinConversation", conversationId);
```

---

**Tài liệu**: COM-4.8.6, COM-4.8.7, COM-4.8.8, BR-25
