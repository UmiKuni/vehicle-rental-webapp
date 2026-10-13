import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { updateUserSchema, type UpdateUserFormValues } from '../schemas/user.schema'
import { useUpdateUser } from '../hooks/useUsers'
import type { UserResponse } from '@/types/user.types'
import { Button } from '@/shared/components/ui/Button'
import { Input } from '@/shared/components/ui/Input'
import { Select } from '@/shared/components/ui/Select'
import { FormField } from '@/shared/components/ui/FormField'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/shared/components/ui/Dialog'

interface EditUserDialogProps {
  user: UserResponse | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function EditUserDialog({ user, open, onOpenChange }: EditUserDialogProps) {
  const { mutate: updateUser, isPending } = useUpdateUser(user?.id ?? 0)

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<UpdateUserFormValues>({
    resolver: zodResolver(updateUserSchema),
  })

  // Populate form when user changes
  useEffect(() => {
    if (user) {
      reset({
        email: user.email,
        fullName: user.fullName,
        phone: user.phone ?? '',
        role: user.role,
        status: user.status,
      })
    }
  }, [user, reset])

  const onSubmit = (values: UpdateUserFormValues) => {
    updateUser(
      { ...values, phone: values.phone || undefined },
      { onSuccess: () => onOpenChange(false) }
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Chỉnh sửa người dùng</DialogTitle>
        </DialogHeader>

        {user && (
          <form onSubmit={handleSubmit(onSubmit)} className="mt-4 space-y-4" noValidate>
            <FormField label="Họ và tên" error={errors.fullName?.message} required>
              <Input id="edit-fullname" {...register('fullName')} />
            </FormField>

            <FormField label="Email" error={errors.email?.message} required>
              <Input id="edit-email" type="email" {...register('email')} />
            </FormField>

            <FormField label="Số điện thoại" error={errors.phone?.message}>
              <Input id="edit-phone" type="tel" {...register('phone')} />
            </FormField>

            <div className="grid grid-cols-2 gap-4">
              <FormField label="Vai trò" error={errors.role?.message} required>
                <Select id="edit-role" {...register('role')}>
                  <option value="STAFF">Nhân viên</option>
                  <option value="MANAGER">Quản lý</option>
                  <option value="ADMIN">Admin</option>
                </Select>
              </FormField>

              <FormField label="Trạng thái" error={errors.status?.message} required>
                <Select id="edit-status" {...register('status')}>
                  <option value="ACTIVE">Hoạt động</option>
                  <option value="INACTIVE">Không hoạt động</option>
                  <option value="BANNED">Đã khoá</option>
                </Select>
              </FormField>
            </div>

            <DialogFooter className="mt-6 gap-2">
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
                Hủy
              </Button>
              <Button type="submit" loading={isPending}>
                Lưu thay đổi
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
