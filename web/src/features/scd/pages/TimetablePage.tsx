import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Modal, Select } from 'antd';
import { InlineAlert } from '@/shared/components/InlineAlert';
import { message } from '@/shared/lib/antdStatic';
import { Eye, FileDown, FileUp, Rocket } from 'lucide-react';
import { useEffect, useState } from 'react';
import { getErrorMessage } from '@/app/apiClient';
import { useCurrentYear, useYears } from '@/features/sch/hooks/useCatalogs';
import { DataTable } from '@/shared/components/DataTable';
import { FileButton } from '@/shared/components/FileButton';
import { FilterBar } from '@/shared/components/FilterBar';
import { ImportErrorPanel } from '@/shared/components/ImportErrorPanel';
import { PageHeader } from '@/shared/components/PageHeader';
import { RowActions } from '@/shared/components/RowActions';
import { StatusBadge } from '@/shared/components/StatusBadge';
import { blobErrorMessage, downloadFile } from '@/shared/lib/download';
import { showError } from '@/shared/lib/feedback';
import { formatDateTime } from '@/shared/lib/format';
import dayjs from 'dayjs';
import { scdApi } from '../api';
import { PublishPreviewModal } from '../components/PublishPreviewModal';
import { TimetableGrid } from '../components/TimetableGrid';
import type { TimetableImportResult, TimetableVersion } from '../types';

/** SCD-01: tải mẫu → sửa → tải lên → xem trước → Phát hành. Mỗi phiên bản là hồ sơ của một lần import, đọc lại được như lịch sử. */
export function TimetablePage() {
  const qc = useQueryClient();
  const campuses = useQuery({ queryKey: ['scd', 'campuses'], queryFn: scdApi.campuses });
  const years = useYears();
  const { current } = useCurrentYear();

  const [campusId, setCampusId] = useState<number | undefined>();
  const [yearId, setYearId] = useState<number | undefined>();
  const [semesterId, setSemesterId] = useState<number | undefined>();

  const effectiveCampus = campusId ?? campuses.data?.[0]?.campusId;
  const effectiveYearId = yearId ?? current?.academicYearId;
  const year = years.data?.find((y) => y.academicYearId === effectiveYearId);
  const defaultSemester = year?.semesters.find((s) => dayjs().isAfter(dayjs(s.startDate).subtract(1, 'day')) && dayjs().isBefore(dayjs(s.endDate).add(1, 'day')))
    ?? year?.semesters[0];
  const effectiveSemester = year?.semesters.some((s) => s.semesterId === semesterId) ? semesterId : defaultSemester?.semesterId;

  useEffect(() => { setSemesterId(undefined); }, [yearId]);

  const ready = effectiveCampus !== undefined && effectiveSemester !== undefined;
  const versions = useQuery({
    queryKey: ['scd', 'versions', effectiveCampus, effectiveSemester],
    queryFn: () => scdApi.versions(effectiveCampus!, effectiveSemester!),
    enabled: ready,
  });

  const [uploading, setUploading] = useState(false);
  const [rejected, setRejected] = useState<TimetableImportResult | null>(null);
  const [gridOf, setGridOf] = useState<TimetableVersion | null>(null);
  const [previewId, setPreviewId] = useState<number | null>(null);
  const grid = useQuery({ queryKey: ['scd', 'grid', gridOf?.versionId], queryFn: () => scdApi.versionGrid(gridOf!.versionId), enabled: gridOf !== null });

  const template = async () => {
    try {
      await downloadFile('/scd/timetable/template', { campusId: effectiveCampus, semesterId: effectiveSemester });
    } catch (e) {
      showError({ response: { data: { message: await blobErrorMessage(e) ?? getErrorMessage(e) } } }, 'Không tải được file mẫu');
    }
  };

  const upload = async (file: File) => {
    setUploading(true);
    setRejected(null);
    try {
      const res = await scdApi.import(file);
      if (res.accepted) {
        await qc.invalidateQueries({ queryKey: ['scd', 'versions'] });
        message.success(`File hợp lệ, đã lưu thành phiên bản Nháp (${res.entryCount} ô).`);
        setPreviewId(res.versionId);
      } else {
        setRejected(res);
      }
    } catch (e) {
      showError(e, 'Không tải lên được');
    } finally {
      setUploading(false);
    }
  };

  const noCampus = campuses.isSuccess && campuses.data.length === 0;

  return (
    <>
      <PageHeader
        title="Thời khóa biểu"
        description="Tải mẫu điền sẵn theo phiên bản đã phát hành gần nhất, sửa vài ô, tải lên rồi xem trước và phát hành."
        extra={
          <>
            <Button icon={<FileDown size={16} />} disabled={!ready} onClick={template}>Tải file mẫu</Button>
            <FileButton type="primary" icon={<FileUp size={16} />} disabled={!ready} loading={uploading} onFile={upload}>Tải lên</FileButton>
          </>
        }
      />

      {noCampus && (
        <InlineAlert
          type="warning" title="Chưa có phân hiệu nào để thao tác"
          description="Hiệu phó cần được hiệu trưởng gán phụ trách phân hiệu ở Hồ sơ → Trách nhiệm trước khi import thời khóa biểu."
        />
      )}

      <FilterBar>
        <Select
          placeholder="Phân hiệu" value={effectiveCampus} onChange={setCampusId}
          options={campuses.data?.map((c) => ({ value: c.campusId, label: c.name }))}
        />
        <Select
          placeholder="Năm học" value={effectiveYearId} onChange={setYearId}
          options={years.data?.map((y) => ({ value: y.academicYearId, label: `Năm học ${y.name}` }))}
        />
        <Select
          placeholder="Học kỳ" value={effectiveSemester} onChange={setSemesterId}
          notFoundContent="Năm học này chưa khai học kỳ"
          options={year?.semesters.map((s) => ({ value: s.semesterId, label: s.name }))}
        />
      </FilterBar>

      {rejected && (
        <Card style={{ marginBottom: 16 }}>
          <ImportErrorPanel errors={rejected.errors} totalErrors={rejected.totalErrors} />
        </Card>
      )}

      <Card className="table-card">
        <DataTable<TimetableVersion>
          rowKey="versionId" loading={versions.isLoading || campuses.isLoading || years.isLoading} dataSource={ready ? versions.data : []}
          emptyText={ready ? 'Chưa có phiên bản nào. Tải file mẫu, điền rồi tải lên.' : 'Chọn phân hiệu và học kỳ để xem các phiên bản.'}
          columns={[
            { title: 'Phiên bản', dataIndex: 'versionId', width: 90, className: 'tabular', render: (v: number) => `#${v}` },
            { title: 'Trạng thái', dataIndex: 'status', width: 140, align: 'center', render: (v: string) => <StatusBadge domain="timetableVersion" value={v} /> },
            { title: 'File', dataIndex: 'sourceFileName', render: (v: string | null) => v ?? <span className="muted">—</span> },
            { title: 'Số ô', dataIndex: 'entryCount', width: 80, align: 'right', className: 'tabular' },
            { title: 'Import lúc', dataIndex: 'importedAt', width: 150, className: 'tabular', render: (v: string) => formatDateTime(v) },
            { title: 'Người import', dataIndex: 'importedByName', width: 150, render: (v: string | null) => v ?? '—' },
            { title: 'Phát hành lúc', dataIndex: 'publishedAt', width: 150, className: 'tabular', render: (v: string | null) => formatDateTime(v) },
            { title: 'Xóa / tạo', key: 'counts', width: 110, align: 'right', className: 'tabular', render: (_: unknown, v: TimetableVersion) => v.status === 'Published' ? `${v.deletedSessionCount} / ${v.createdSessionCount}` : '—' },
            {
              title: 'Thao tác', key: 'actions', width: 110, align: 'center', fixed: 'right',
              render: (_: unknown, v: TimetableVersion) => (
                <RowActions actions={[
                  { key: 'grid', label: 'Xem lưới', icon: <Eye size={16} />, onClick: () => setGridOf(v) },
                  { key: 'publish', label: 'Xem trước và phát hành', icon: <Rocket size={16} />, hidden: v.status !== 'Draft', onClick: () => setPreviewId(v.versionId) },
                ]} />
              ),
            },
          ]}
        />
      </Card>

      <PublishPreviewModal versionId={previewId} onClose={() => setPreviewId(null)} />

      <Modal
        open={gridOf !== null} width={1100} onCancel={() => setGridOf(null)} mask={{ closable: false }}
        title={gridOf ? `Phiên bản #${gridOf.versionId} — ${gridOf.campusName}, ${gridOf.semesterName}` : ''}
        footer={<Button onClick={() => setGridOf(null)}>Đóng</Button>}
      >
        {grid.isLoading && <DataTable<object> rowKey="x" loading dataSource={[]} columns={[]} />}
        {grid.data && <TimetableGrid grid={grid.data} />}
      </Modal>
    </>
  );
}
