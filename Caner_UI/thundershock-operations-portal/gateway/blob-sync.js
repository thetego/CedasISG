import { ContainerClient } from "@azure/storage-blob";
import { parquetReadObjects } from "hyparquet";
import { compressors } from "hyparquet-compressors";

function arrayBuffer(buffer) {
  return buffer.buffer.slice(buffer.byteOffset, buffer.byteOffset + buffer.byteLength);
}

export function asyncBufferFromNodeBuffer(buffer) {
  return {
    byteLength: buffer.byteLength,
    slice(start, end) {
      return arrayBuffer(buffer.subarray(start, end));
    },
  };
}

export class BlobSync {
  constructor(containerUrl, store, retentionDays) {
    this.client = containerUrl ? new ContainerClient(containerUrl) : null;
    this.store = store;
    this.retentionDays = retentionDays;
    this.running = false;
    this.lastSyncAt = null;
    this.lastError = null;
  }

  async sync() {
    if (!this.client || this.running) return { skipped: true };
    this.running = true;
    const summary = { blobs: 0, rows: 0, accepted: 0, rejected: 0, duplicate: 0 };
    try {
      for await (const blob of this.client.listBlobsFlat()) {
        if (!blob.name.toLowerCase().endsWith(".parquet")) continue;
        const etag = blob.properties.etag || "";
        if (this.store.isBlobProcessed(blob.name, etag)) continue;
        const buffer = await this.client.getBlockBlobClient(blob.name).downloadToBuffer();
        const rows = await parquetReadObjects({
          file: asyncBufferFromNodeBuffer(buffer),
          compressors,
        });
        const result = this.store.ingest(rows, blob.name);
        this.store.markBlobProcessed(blob.name, etag, rows.length);
        summary.blobs++;
        summary.rows += rows.length;
        summary.accepted += result.accepted;
        summary.rejected += result.rejected;
        summary.duplicate += result.duplicate;
      }
      this.store.prune(this.retentionDays);
      this.lastSyncAt = new Date().toISOString();
      this.lastError = null;
      return summary;
    } catch (error) {
      this.lastError = error.message;
      throw error;
    } finally {
      this.running = false;
    }
  }
}
