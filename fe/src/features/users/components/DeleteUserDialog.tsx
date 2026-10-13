import { Trash2 } from 'lucide-react'
import { useDeleteUser } from '../hooks/useUsers'
import type { UserResponse } from '@/types/user.types'
import { Button } from '@/shared/components/ui/Button'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/shared/components/ui/Dialog'

interface DeleteUserDialogProps {
  user: UserResponse | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function DeleteUserDialog({ user, open, onOpenChange }: DeleteUserDialogProps) {
  const { mutate: deleteUser, isPending } = useDeleteUser()

  const handleDelete = () => {
    if (!user) return
    deleteUser(user.id, {
      onSuccess: () => onOpenChange(false),
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-destructive/10">
            <Trash2 className="size-6 text-destructive" />
          </div>
          <DialogTitle className="text-center">Xác nhận xoá</DialogTitle>
          <DialogDescription className="text-center">
            Bạn có chắc muốn xoá người dùng{' '}
            <span className="font-semibold text-foreground">{user?.fullName}</span>? Hành động này
            không thể hoàn tác.
          </DialogDescription>
        </DialogHeader>

        <DialogFooter className="mt-4 gap-2">
          <Button variant="outline" onClick={() => onOpenChange(false)} className="flex-1">
            Hủy
          </Button>
          <Button
            variant="destructive"
            onClick={handleDelete}
            loading={isPending}
            className="flex-1"
          >
            Xoá
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
