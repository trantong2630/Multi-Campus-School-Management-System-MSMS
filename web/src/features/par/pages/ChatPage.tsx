import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Avatar,
  Badge,
  Button,
  Empty,
  Form,
  Input,
  Modal,
  Select,
  Spin,
  Tag,
  Tooltip,
} from 'antd';
import {
  Lock,
  MessageSquare,
  MessageSquarePlus,
  Send,
  UserCheck,
} from 'lucide-react';
import * as signalR from '@microsoft/signalr';
import { tokenStore } from '@/app/apiClient';
import { useAuth } from '@/app/AuthProvider';
import { PageHeader } from '@/shared/components/PageHeader';
import { showError } from '@/shared/lib/feedback';
import { formatDateTime } from '@/shared/lib/format';
import { accent } from '@/shared/lib/tokens';
import type { AccentName } from '@/shared/lib/tokens';
import { conversationApi, leaveApi } from '../api';
import type {
  AvailableParentItem,
  AvailableTeacherItem,
  ConversationItem,
  MessageItem,
  MyChildItem,
  TeachingStudentItem,
} from '../types';
import './chat.css';

const TONES: AccentName[] = ['green', 'sky', 'violet', 'amber', 'teal', 'coral'];

function toneOf(name: string): AccentName {
  let h = 0;
  for (const ch of name) h = (h * 31 + ch.charCodeAt(0)) >>> 0;
  return TONES[h % TONES.length];
}

export function ChatPage() {
  const { user } = useAuth();
  const isParent = user?.accountType === 'Parent';

  // --- State danh sách hội thoại ---
  const [conversations, setConversations] = useState<ConversationItem[]>([]);
  const [loadingConversations, setLoadingConversations] = useState<boolean>(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [searchQuery, setSearchQuery] = useState<string>('');

  // --- State tin nhắn của hội thoại hiện tại ---
  const [messages, setMessages] = useState<MessageItem[]>([]);
  const [loadingMessages, setLoadingMessages] = useState<boolean>(false);
  const [inputContent, setInputContent] = useState<string>('');
  const [sending, setSending] = useState<boolean>(false);
  const [isPartnerTyping, setIsPartnerTyping] = useState<boolean>(false);

  // --- State Modal tạo hội thoại mới ---
  const [createModalOpen, setCreateModalOpen] = useState<boolean>(false);
  const [creating, setCreating] = useState<boolean>(false);
  const [form] = Form.useForm();

  // Dữ liệu hỗ trợ modal
  const [childrenList, setChildrenList] = useState<MyChildItem[]>([]);
  const [availableTeachers, setAvailableTeachers] = useState<AvailableTeacherItem[]>([]);
  const [loadingTeachers, setLoadingTeachers] = useState<boolean>(false);

  const [teachingStudents, setTeachingStudents] = useState<TeachingStudentItem[]>([]);
  const [availableParents, setAvailableParents] = useState<AvailableParentItem[]>([]);
  const [loadingParents, setLoadingParents] = useState<boolean>(false);
  const [selectedStudentId, setSelectedStudentId] = useState<number | null>(null);

  // --- Refs & SignalR connection ---
  const hubRef = useRef<signalR.HubConnection | null>(null);
  const messagesEndRef = useRef<HTMLDivElement | null>(null);
  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const activeConversationIdRef = useRef<number | null>(null);
  activeConversationIdRef.current = selectedId;

  const activeConversation = useMemo(
    () => conversations.find((c) => c.conversationId === selectedId) ?? null,
    [conversations, selectedId]
  );

  // Cuộn xuống tin nhắn cuối
  const scrollToBottom = (smooth = true) => {
    messagesEndRef.current?.scrollIntoView({ behavior: smooth ? 'smooth' : 'auto' });
  };

  // Tải danh sách hội thoại
  const loadConversations = useCallback(async (autoSelectFirst = false) => {
    try {
      setLoadingConversations(true);
      const data = await conversationApi.getMyConversations();
      setConversations(data);

      if (autoSelectFirst && data.length > 0 && selectedId === null) {
        setSelectedId(data[0].conversationId);
      }
    } catch (err) {
      showError(err, 'Không thể tải danh sách hội thoại');
    } finally {
      setLoadingConversations(false);
    }
  }, [selectedId]);

  // Tải tin nhắn của hội thoại được chọn
  const loadMessages = useCallback(async (conversationId: number) => {
    try {
      setLoadingMessages(true);
      const data = await conversationApi.getMessages(conversationId);
      setMessages(data);

      // Cập nhật lại unreadCount về 0 trong danh sách bên trái
      setConversations((prev) =>
        prev.map((c) => (c.conversationId === conversationId ? { ...c, unreadCount: 0 } : c))
      );

      setTimeout(() => scrollToBottom(false), 50);
    } catch (err) {
      showError(err, 'Không thể tải tin nhắn');
    } finally {
      setLoadingMessages(false);
    }
  }, []);

  // --- Khởi tạo và quản lý SignalR Hub ---
  useEffect(() => {
    const token = tokenStore.get();
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/signalr', {
        accessTokenFactory: () => tokenStore.get() ?? '',
      })
      .withAutomaticReconnect()
      .build();

    // 1. Nhận tin nhắn mới
    connection.on('MessageReceived', (msg: {
      messageId: number;
      conversationId: number;
      senderPersonId: number;
      senderName: string;
      content: string;
      sentAt: string;
      readAt: string | null;
    }) => {
      const currentActiveId = activeConversationIdRef.current;
      const isCurrent = msg.conversationId === currentActiveId;

      if (isCurrent) {
        // Thêm vào danh sách tin nhắn đang mở nếu chưa tồn tại
        const isMine = msg.senderPersonId === user?.personId;
        const newItem: MessageItem = {
          messageId: msg.messageId,
          conversationId: msg.conversationId,
          senderPersonId: msg.senderPersonId,
          senderName: msg.senderName,
          isMine,
          content: msg.content,
          sentAt: msg.sentAt,
          readAt: msg.readAt,
        };

        setMessages((prev) => {
          if (prev.some((m) => m.messageId === newItem.messageId)) return prev;
          return [...prev, newItem];
        });
        setIsPartnerTyping(false);
        setTimeout(() => scrollToBottom(true), 50);

        // Nếu tin nhắn của đối phương gửi tới phòng đang mở, tự động đánh dấu đã đọc
        if (!isMine) {
          conversationApi.getMessages(msg.conversationId).catch(() => { });
        }
      }

      // Cập nhật lại snippet tin nhắn cuối trong danh sách hội thoại
      setConversations((prev) =>
        prev.map((c) => {
          if (c.conversationId === msg.conversationId) {
            return {
              ...c,
              lastMessageContent: msg.content,
              lastMessageAt: msg.sentAt,
              unreadCount: isCurrent || msg.senderPersonId === user?.personId ? 0 : c.unreadCount + 1,
            };
          }
          return c;
        })
      );
    });

    // 2. Nhận sự kiện Đã xem (MessagesRead)
    connection.on('MessagesRead', (conversationId: number, readAt: string) => {
      if (conversationId === activeConversationIdRef.current) {
        setMessages((prev) =>
          prev.map((m) => (m.isMine && !m.readAt ? { ...m, readAt } : m))
        );
      }
    });

    // 3. Nhận sự kiện đang soạn tin (UserTyping)
    connection.on('UserTyping', (conversationId: number, personId: number, isTyping: boolean) => {
      if (conversationId === activeConversationIdRef.current && personId !== user?.personId) {
        setIsPartnerTyping(isTyping);
      }
    });

    connection.onreconnected(() => {
      if (activeConversationIdRef.current) {
        connection.invoke('JoinConversation', activeConversationIdRef.current).catch(() => { });
      }
    });

    connection
      .start()
      .then(() => {
        hubRef.current = connection;
        // Nếu đã có conversation đang chọn, join ngay vào group
        if (activeConversationIdRef.current) {
          connection.invoke('JoinConversation', activeConversationIdRef.current).catch(() => { });
        }
      })
      .catch((err) => {
        console.warn('Lỗi kết nối SignalR:', err);
      });

    return () => {
      connection.stop();
      hubRef.current = null;
    };
  }, [user?.personId]);

  // Khi chọn hội thoại khác: Tải tin nhắn & Join group SignalR
  useEffect(() => {
    if (selectedId) {
      loadMessages(selectedId);
      setIsPartnerTyping(false);

      if (hubRef.current && hubRef.current.state === signalR.HubConnectionState.Connected) {
        hubRef.current.invoke('JoinConversation', selectedId).catch(() => { });
      }
    } else {
      setMessages([]);
    }
  }, [selectedId, loadMessages]);

  // Khởi chạy ban đầu: Tải danh sách hội thoại
  useEffect(() => {
    loadConversations(true);
  }, [loadConversations]);

  // Xử lý gửi tin nhắn
  const handleSendMessage = async () => {
    const content = inputContent.trim();
    if (!content || !selectedId || sending) return;

    if (activeConversation?.isReadOnly) {
      showError('Hội thoại đã ở chế độ chỉ đọc. Không thể gửi tin.');
      return;
    }

    try {
      setSending(true);

      // Báo dừng soạn tin
      if (hubRef.current && hubRef.current.state === signalR.HubConnectionState.Connected) {
        hubRef.current.invoke('SendTyping', selectedId, false).catch(() => { });
      }

      const sentMsg = await conversationApi.sendMessage(selectedId, content);
      setInputContent('');

      // Cập nhật ngay tin nhắn vừa gửi vào danh sách hiển thị
      if (sentMsg) {
        setMessages((prev) => {
          if (prev.some((m) => m.messageId === sentMsg.messageId)) return prev;
          return [...prev, sentMsg];
        });

        // Cập nhật lại snippet tin nhắn cuối ở danh sách hội thoại bên trái
        setConversations((prev) =>
          prev.map((c) =>
            c.conversationId === selectedId
              ? {
                ...c,
                lastMessageContent: sentMsg.content,
                lastMessageAt: sentMsg.sentAt,
              }
              : c
          )
        );

        setTimeout(() => scrollToBottom(true), 50);
      }
    } catch (err) {
      showError(err, 'Không gửi được tin nhắn');
    } finally {
      setSending(false);
    }
  };

  // Xử lý gõ phím & báo typing
  const handleInputChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const val = e.target.value;
    setInputContent(val);

    if (!selectedId || activeConversation?.isReadOnly) return;

    if (hubRef.current && hubRef.current.state === signalR.HubConnectionState.Connected) {
      hubRef.current.invoke('SendTyping', selectedId, true).catch(() => { });

      if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
      typingTimeoutRef.current = setTimeout(() => {
        if (hubRef.current && hubRef.current.state === signalR.HubConnectionState.Connected && selectedId) {
          hubRef.current.invoke('SendTyping', selectedId, false).catch(() => { });
        }
      }, 2500);
    }
  };

  // Xử lý phím Enter để gửi (Shift+Enter để xuống dòng)
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  // --- Mở Modal Tạo Hội Thoại ---
  const handleOpenCreateModal = async () => {
    form.resetFields();
    setAvailableTeachers([]);
    setAvailableParents([]);
    setSelectedStudentId(null);
    setCreateModalOpen(true);

    try {
      if (isParent) {
        const children = await leaveApi.getMyChildren();
        setChildrenList(children);
        if (children.length === 1) {
          form.setFieldValue('studentId', children[0].studentId);
          handleChildChange(children[0].studentId);
        }
      } else {
        const students = await conversationApi.getMyTeachingStudents();
        setTeachingStudents(students);
      }
    } catch (err) {
      showError(err, 'Không tải được danh sách khởi tạo');
    }
  };

  // Khi Phụ huynh chọn con
  const handleChildChange = async (studentId: number) => {
    form.setFieldValue('staffId', undefined);
    setAvailableTeachers([]);
    setSelectedStudentId(studentId ?? null);
    if (!studentId) return;

    try {
      setLoadingTeachers(true);
      const teachers = await conversationApi.getAvailableTeachers(studentId);
      setAvailableTeachers(teachers);
      if (teachers.length === 1) {
        form.setFieldValue('staffId', teachers[0].staffId);
      }
    } catch (err) {
      showError(err, 'Không tải được danh sách giáo viên của học sinh');
    } finally {
      setLoadingTeachers(false);
    }
  };

  // Khi Giáo viên chọn học sinh
  const handleStudentChange = async (studentId: number) => {
    form.setFieldValue('parentId', undefined);
    setAvailableParents([]);
    setSelectedStudentId(studentId ?? null);
    if (!studentId) return;

    try {
      setLoadingParents(true);
      const parents = await conversationApi.getAvailableParents(studentId);
      setAvailableParents(parents);
      if (parents.length === 1) {
        form.setFieldValue('parentId', parents[0].parentId);
      }
    } catch (err) {
      showError(err, 'Không tải được danh sách phụ huynh của học sinh');
    } finally {
      setLoadingParents(false);
    }
  };

  // Submit Modal tạo hội thoại
  const handleCreateSubmit = async () => {
    // Nếu form chưa nhập đủ hoặc lỗi validation, validateFields().catch() bắt lại và dừng,
    // tránh hiển thị hộp thoại modal lỗi chung chung "Có lỗi xảy ra"
    const values = await form.validateFields().catch(() => null);
    if (!values) return;

    try {
      setCreating(true);

      const conversationId = await conversationApi.createConversation({
        studentId: values.studentId,
        staffId: values.staffId,
        parentId: values.parentId,
      });

      setCreateModalOpen(false);
      await loadConversations(false);
      setSelectedId(conversationId);
    } catch (err) {
      showError(err, 'Không thể tạo cuộc hội thoại');
    } finally {
      setCreating(false);
    }
  };

  // Bỏ dấu và đưa về chữ thường để gõ "nguyen van" vẫn ra "Nguyễn Văn"
  const fold = (s: string) =>
    s.normalize('NFD').replace(/\p{M}/gu, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

  // Lọc danh sách hội thoại theo từ khóa tìm kiếm (hỗ trợ tiếng Việt không dấu & đa tiêu chí)
  const filteredConversations = useMemo(() => {
    const q = fold(searchQuery.trim());
    if (!q) return conversations;

    return conversations.filter((c) => {
      // Tìm theo đối tác chat (Phụ huynh tìm Giáo viên, Giáo viên tìm Phụ huynh) để tránh trùng tên chính mình
      const partnerFields = isParent
        ? [c.staffName, c.staffAbbr ?? '', c.roleDescription ?? '']
        : [c.parentName ?? '', `Phụ huynh em ${c.studentName}`];

      const searchFields = [
        ...partnerFields,
        c.studentName,
        c.studentCode,
        c.className,
      ];

      return searchFields.some((field) => fold(field).includes(q));
    });
  }, [conversations, searchQuery, isParent]);

  // Xác định tên người đối diện và nhãn vai trò hiển thị
  const getPartnerDisplay = (c: ConversationItem) => {
    if (isParent) {
      return {
        name: c.staffName,
        initial: c.staffName.trim().split(/\s+/).at(-1)?.[0]?.toUpperCase() ?? 'G',
        role: c.roleDescription,
        studentSub: `HS: ${c.studentName} (${c.className})`,
      };
    }
    // Giáo viên xem: hiển thị tên thực tế của phụ huynh (nếu có)
    const displayName = c.parentName?.trim() || `Phụ huynh em ${c.studentName}`;
    return {
      name: displayName,
      initial: displayName.trim().split(/\s+/).at(-1)?.[0]?.toUpperCase() ?? 'P',
      role: `Lớp ${c.className}`,
      studentSub: `HS: ${c.studentName} (${c.className}) - ${c.studentCode}`,
    };
  };

  const partnerHeader = activeConversation ? getPartnerDisplay(activeConversation) : null;

  return (
    <div className="page-container">
      <PageHeader
        title="Hộp thư Phụ huynh – Giáo viên"
        description="Trao đổi trực tiếp 1-1 giữa Phụ huynh và Giáo viên phụ trách giảng dạy"
      />

      <div className="chat-container">
        {/* ===================== CỘT TRÁI: DANH SÁCH HỘI THOẠI ===================== */}
        <div className="chat-sidebar">
          <div className="chat-sidebar__header">
            <h2 className="chat-sidebar__title">Danh sách hội thoại</h2>
            <Button
              type="primary"
              size="small"
              icon={<MessageSquarePlus size={16} />}
              onClick={handleOpenCreateModal}
            >
              Hội thoại mới
            </Button>
          </div>

          <div className="chat-sidebar__search">
            <Input.Search
              placeholder="Tìm theo tên người, học sinh, lớp..."
              allowClear
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              onSearch={(val) => setSearchQuery(val)}
            />
          </div>

          <div className="chat-sidebar__list">
            {loadingConversations ? (
              <div style={{ textAlign: 'center', padding: '40px 0' }}>
                <Spin tip="Đang tải hội thoại..." />
              </div>
            ) : filteredConversations.length === 0 ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description={searchQuery ? `Không tìm thấy hội thoại phù hợp với "${searchQuery}"` : 'Chưa có cuộc trò chuyện nào'}
                style={{ margin: '40px 0' }}
              >
                {searchQuery ? (
                  <Button size="small" onClick={() => setSearchQuery('')}>
                    Xóa tìm kiếm
                  </Button>
                ) : (
                  <Button type="primary" size="small" onClick={handleOpenCreateModal}>
                    Bắt đầu trò chuyện
                  </Button>
                )}
              </Empty>
            ) : (
              filteredConversations.map((c) => {
                const info = getPartnerDisplay(c);
                const isSelected = c.conversationId === selectedId;
                const tone = accent[toneOf(info.name)];

                return (
                  <div
                    key={c.conversationId}
                    className={`chat-conv-item ${isSelected ? 'chat-conv-item--active' : ''} ${c.unreadCount > 0 ? 'chat-conv-item--unread' : ''
                      }`}
                    onClick={() => setSelectedId(c.conversationId)}
                  >
                    <div className="chat-conv-item__avatar-wrap">
                      <Avatar size={42} style={{ background: tone.bg, color: tone.fg, fontWeight: 600 }}>
                        {info.initial}
                      </Avatar>
                    </div>

                    <div className="chat-conv-item__content">
                      <div className="chat-conv-item__top">
                        <span className="chat-conv-item__name" title={info.name}>
                          {info.name}
                        </span>
                        <span className="chat-conv-item__time">
                          {c.lastMessageAt ? formatDateTime(c.lastMessageAt).split(' ')[1] : ''}
                        </span>
                      </div>

                      <div className="chat-conv-item__meta">
                        <Tag color="blue" bordered={false} style={{ margin: 0, fontSize: 11 }}>
                          {info.role}
                        </Tag>
                        {c.isReadOnly && (
                          <Tag color="warning" bordered={false} style={{ margin: 0, fontSize: 11 }}>
                            Chỉ đọc
                          </Tag>
                        )}
                        <span className="chat-conv-item__student">{info.studentSub}</span>
                      </div>

                      <div className="chat-conv-item__bottom">
                        <span className="chat-conv-item__snippet" title={c.lastMessageContent ?? 'Chưa có tin nhắn'}>
                          {c.lastMessageContent ?? 'Bắt đầu cuộc trò chuyện...'}
                        </span>
                        {c.unreadCount > 0 && <Badge count={c.unreadCount} size="small" />}
                      </div>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

        {/* ===================== CỘT PHẢI: KHUNG CHAT 1-1 ===================== */}
        <div className="chat-main">
          {activeConversation && partnerHeader ? (
            <>
              {/* Header khung chat */}
              <div className="chat-main__header">
                <div className="chat-main__header-info">
                  <Avatar
                    size={42}
                    style={{
                      background: accent[toneOf(partnerHeader.name)].bg,
                      color: accent[toneOf(partnerHeader.name)].fg,
                      fontWeight: 600,
                    }}
                  >
                    {partnerHeader.initial}
                  </Avatar>
                  <div className="chat-main__header-text">
                    <h3>
                      <span>{partnerHeader.name}</span>
                      <Tag color="blue" bordered={false}>
                        {partnerHeader.role}
                      </Tag>
                      <Tooltip title="Đang hoạt động trong hệ thống">
                        <span className="status-dot status-dot--online" />
                      </Tooltip>
                    </h3>
                    <div className="chat-main__header-sub">
                      <span>Trao đổi về học sinh: <strong>{activeConversation.studentName}</strong> ({activeConversation.className})</span>
                      {isPartnerTyping && (
                        <span className="chat-typing-indicator">
                          <span>•</span> Đang nhập tin nhắn...
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {/* Danh sách tin nhắn */}
              <div className="chat-messages">
                {loadingMessages ? (
                  <div style={{ textAlign: 'center', padding: '60px 0' }}>
                    <Spin tip="Đang tải lịch sử tin nhắn..." />
                  </div>
                ) : messages.length === 0 ? (
                  <div className="chat-main__empty">
                    <MessageSquare size={48} strokeWidth={1.5} color="#bfbfbf" />
                    <p style={{ marginTop: 12 }}>Chưa có tin nhắn nào trong cuộc trò chuyện này.</p>
                    <p style={{ fontSize: 13, color: '#8c8c8c' }}>Hãy gửi tin nhắn đầu tiên để bắt đầu trao đổi!</p>
                  </div>
                ) : (
                  messages.map((m, idx) => {
                    const isLastMine = m.isMine && idx === messages.length - 1;

                    return (
                      <div
                        key={m.messageId}
                        className={`chat-message-row ${m.isMine ? 'chat-message-row--mine' : 'chat-message-row--theirs'
                          }`}
                      >
                        <div className="chat-bubble">{m.content}</div>
                        <div className="chat-message-time">
                          <span>{formatDateTime(m.sentAt)}</span>
                          {isLastMine && m.readAt && (
                            <span className="chat-message-seen" title={`Đã xem lúc ${formatDateTime(m.readAt)}`}>
                              <UserCheck size={12} style={{ display: 'inline', marginRight: 2 }} />
                              Đã xem
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })
                )}
                <div ref={messagesEndRef} />
              </div>

              {/* Khung nhập tin nhắn & Cảnh báo chỉ đọc */}
              <div className="chat-footer">
                {activeConversation.isReadOnly && (
                  <Alert
                    type="warning"
                    showIcon
                    icon={<Lock size={16} />}
                    className="chat-readonly-alert"
                    message="Quan hệ giảng dạy đã kết thúc. Hộp thư này đã chuyển sang chế độ chỉ đọc."
                  />
                )}

                <div className="chat-input-bar">
                  <Input.TextArea
                    placeholder={
                      activeConversation.isReadOnly
                        ? 'Hội thoại đã ở chế độ chỉ đọc. Không thể gửi tin.'
                        : 'Nhập tin nhắn (Enter để gửi, Shift+Enter để xuống dòng)...'
                    }
                    disabled={activeConversation.isReadOnly || sending}
                    value={inputContent}
                    onChange={handleInputChange}
                    onKeyDown={handleKeyDown}
                    autoSize={{ minRows: 2, maxRows: 4 }}
                    maxLength={2000}
                    showCount={!activeConversation.isReadOnly}
                  />

                  {!activeConversation.isReadOnly && (
                    <Button
                      type="primary"
                      className="chat-input-bar__send"
                      icon={<Send size={16} />}
                      loading={sending}
                      disabled={!inputContent.trim()}
                      onClick={handleSendMessage}
                    >
                      Gửi
                    </Button>
                  )}
                </div>
              </div>
            </>
          ) : (
            <div className="chat-main__empty">
              <MessageSquare size={64} strokeWidth={1} color="#d9d9d9" />
              <h3 style={{ marginTop: 16, color: '#595959' }}>Chưa chọn cuộc hội thoại</h3>
              <p style={{ color: '#8c8c8c' }}>Chọn một hội thoại bên trái hoặc bấm "Hội thoại mới" để bắt đầu trao đổi.</p>
              <Button type="primary" icon={<MessageSquarePlus size={16} />} onClick={handleOpenCreateModal}>
                Tạo hội thoại mới
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* ===================== MODAL: TẠO HỘI THOẠI MỚI ===================== */}
      <Modal
        title="Bắt đầu cuộc trò chuyện mới"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        onOk={handleCreateSubmit}
        confirmLoading={creating}
        okText="Bắt đầu trò chuyện"
        cancelText="Hủy"
        destroyOnClose
        okButtonProps={{
          disabled:
            (!isParent && !!selectedStudentId && !loadingParents && availableParents.length === 0) ||
            (isParent && !!selectedStudentId && !loadingTeachers && availableTeachers.length === 0),
        }}
      >
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          {isParent ? (
            <>
              <Form.Item
                name="studentId"
                label="Chọn con"
                rules={[{ required: true, message: 'Vui lòng chọn học sinh' }]}
              >
                <Select
                  placeholder="Chọn học sinh cần trao đổi..."
                  onChange={handleChildChange}
                  options={childrenList.map((c) => ({
                    value: c.studentId,
                    label: `${c.fullName} (${c.studentCode}) - Lớp ${c.className ?? 'Chưa xếp lớp'}`,
                  }))}
                />
              </Form.Item>

              <Form.Item
                name="staffId"
                label="Chọn Giáo viên phụ trách"
                rules={[{ required: true, message: 'Vui lòng chọn giáo viên' }]}
              >
                <Select
                  placeholder={
                    loadingTeachers
                      ? 'Đang tải danh sách giáo viên...'
                      : selectedStudentId && availableTeachers.length === 0
                        ? 'Không có giáo viên phụ trách học kỳ này'
                        : 'Chọn giáo viên phụ trách...'
                  }
                  loading={loadingTeachers}
                  disabled={!selectedStudentId || loadingTeachers || availableTeachers.length === 0}
                  options={availableTeachers.map((t) => ({
                    value: t.staffId,
                    label: `${t.fullName} (${t.roleDescription})`,
                  }))}
                />
              </Form.Item>

              {selectedStudentId && !loadingTeachers && availableTeachers.length === 0 && (
                <Alert
                  type="warning"
                  showIcon
                  message="Chưa có giáo viên phụ trách"
                  description="Học sinh chưa có giáo viên nào được phân công giảng dạy trong học kỳ này. Bạn không thể tạo cuộc trò chuyện."
                  style={{ marginTop: -8, marginBottom: 16 }}
                />
              )}
            </>
          ) : (
            <>
              <Form.Item
                name="studentId"
                label="Chọn học sinh đang phụ trách"
                rules={[{ required: true, message: 'Vui lòng chọn học sinh' }]}
              >
                <Select
                  showSearch
                  placeholder="Tìm và chọn học sinh..."
                  optionFilterProp="label"
                  onChange={handleStudentChange}
                  options={teachingStudents.map((s) => ({
                    value: s.studentId,
                    label: `${s.fullName} (${s.studentCode}) - Lớp ${s.className}`,
                  }))}
                />
              </Form.Item>

              <Form.Item
                name="parentId"
                label="Chọn Phụ huynh của học sinh"
                rules={[{ required: true, message: 'Vui lòng chọn phụ huynh' }]}
              >
                <Select
                  placeholder={
                    loadingParents
                      ? 'Đang tải danh sách phụ huynh...'
                      : selectedStudentId && availableParents.length === 0
                        ? 'Học sinh chưa có phụ huynh liên kết'
                        : 'Chọn phụ huynh để nhắn tin...'
                  }
                  loading={loadingParents}
                  disabled={!selectedStudentId || loadingParents || availableParents.length === 0}
                  options={availableParents.map((p) => ({
                    value: p.parentId,
                    label: `${p.fullName} (${p.relationship ?? 'Phụ huynh'}${p.phone ? ` - ${p.phone}` : ''})`,
                  }))}
                />
              </Form.Item>

              {selectedStudentId && !loadingParents && availableParents.length === 0 && (
                <Alert
                  type="warning"
                  showIcon
                  message="Học sinh chưa liên kết phụ huynh"
                  description="Học sinh này chưa có phụ huynh nào được liên kết trong hệ thống. Vui lòng liên hệ Giáo vụ hoặc Quản trị viên để cập nhật liên kết phụ huynh cho học sinh trước."
                  style={{ marginTop: -8, marginBottom: 16 }}
                />
              )}
            </>
          )}
        </Form>
      </Modal>
    </div>
  );
}
