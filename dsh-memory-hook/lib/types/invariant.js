/**
 * Package-owned invariant companion for `@wangdefa/dsh-wangdefa-memory-hook`.
 * @module @wangdefa/dsh-wangdefa-memory-hook/invariant
 */
const PACKAGE_NAME = '@wangdefa/dsh-wangdefa-memory-hook';
/** Cordis companion plugin name. */
export const name = 'wangdefa-memory-hook-invariant';
/** Service required before the companion can reserve package ownership. */
export const inject = ['invariants'];
/**
 * No runtime invariant: the observable contract is cross-package (the memory
 * MCP server owns the persisted state, this plugin only relays), while the
 * plugin's in-memory per-agent snapshot is asserted by focused pipeline tests.
 */
const install = () => { };
/**
 * Register this package's invariant companion.
 * @param ctx - Cordis context carrying the invariant service.
 * @returns the installed registration's disposer after setup succeeds.
 */
export const apply = (ctx) => Promise.resolve(ctx.invariants.register(PACKAGE_NAME, install));
/* jscpd:ignore-end */
//# sourceMappingURL=invariant.js.map