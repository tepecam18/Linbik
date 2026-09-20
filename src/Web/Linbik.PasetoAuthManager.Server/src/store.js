/** Single-process example store. Distributed implementations must provide a distributed lock. */
export class MemorySessionStore {
  #records = new Map();
  #locks = new Map();
  constructor({ capacity = 10000 } = {}) { this.capacity = capacity; }
  async get(key) {
    const record = this.#records.get(key);
    if (!record || record.expiresAt <= Date.now()) { this.#records.delete(key); return null; }
    return structuredClone(record);
  }
  async set(key, record) {
    for (const [id, value] of this.#records) if (value.expiresAt <= Date.now()) this.#records.delete(id);
    if (!this.#records.has(key) && this.#records.size >= this.capacity) throw new Error('Session store capacity exceeded.');
    this.#records.set(key, structuredClone(record));
  }
  async delete(key) { this.#records.delete(key); }
  async withLock(key, action) {
    const previous = this.#locks.get(key) ?? Promise.resolve();
    let release;
    const current = new Promise(resolve => { release = resolve; });
    this.#locks.set(key, current);
    await previous;
    try { return await action(); } finally {
      release();
      if (this.#locks.get(key) === current) this.#locks.delete(key);
    }
  }
}
