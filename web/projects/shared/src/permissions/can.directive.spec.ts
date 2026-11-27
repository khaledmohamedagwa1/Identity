import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CanDirective } from './can.directive';
import { PermissionService } from './permission.service';

const granted = signal<ReadonlySet<string>>(new Set());
const fakePermissions = {
  calls: [] as (string | undefined)[][],
  can(permission: string, scope?: string) {
    this.calls.push([permission, scope]);
    return () => granted().has(scope ? `${permission}@${scope}` : permission);
  },
};

@Component({
  imports: [CanDirective],
  template: `
    <button *appCan="'users_manage'; on: nodeId()" data-testid="invite">Invite</button>
    <section *appCan="'roles_manage'; else readOnly" data-testid="editor">Editor</section>
    <ng-template #readOnly><p data-testid="read-only">Read only</p></ng-template>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class HostComponent {
  readonly nodeId = signal('cairo');
}

describe('CanDirective (*appCan)', () => {
  beforeEach(() => {
    granted.set(new Set());
    fakePermissions.calls = [];
    TestBed.configureTestingModule({
      providers: [{ provide: PermissionService, useValue: fakePermissions }],
    });
  });

  const query = (root: HTMLElement, id: string) => root.querySelector(`[data-testid="${id}"]`);

  it('hides content until the permission is granted at the node', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(query(el, 'invite')).toBeNull();
    expect(fakePermissions.calls).toContainEqual(['users_manage', 'cairo']);

    granted.set(new Set(['users_manage@cairo']));
    await fixture.whenStable();
    expect(query(el, 'invite')).not.toBeNull();
  });

  it('re-evaluates when the node changes', async () => {
    granted.set(new Set(['users_manage@cairo']));
    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(query(el, 'invite')).not.toBeNull();

    fixture.componentInstance.nodeId.set('alexandria');
    await fixture.whenStable();
    expect(query(el, 'invite')).toBeNull();
  });

  it('renders the else template when denied', async () => {
    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(query(el, 'editor')).toBeNull();
    expect(query(el, 'read-only')).not.toBeNull();

    granted.set(new Set(['roles_manage']));
    await fixture.whenStable();
    expect(query(el, 'editor')).not.toBeNull();
    expect(query(el, 'read-only')).toBeNull();
  });
});
