import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { createUserSchema, type CreateUserFormValues } from '../schemas/user.schema'
import { useCreateUser } from '../hooks/useUsers'
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

interface CreateUserDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function CreateUserDialog({ open, onOpenChange }: CreateUserDialogProps) {
  const { mutate: createUser, isPending } = useCreateUser()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreateUserFormValues>({
    resolver: zodResolver(createUserSchema),
    defaultValues: { username: '', email: '', password: '', fullName: '', phone: '', role: 'STAFF' },
  })

  const onSubmit = (values: CreateUserFormValues) => {
    createUser(
      { ...values, phone: values.phone || undefined },
      {
        onSuccess: () => {
          reset()
          onOpenChange(false)
        },
      }
    )
  }

  const handleClose = () => {
    reset()
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Thêm người dùng mới</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-4 space-y-4" noValidate>
          <div className="grid grid-cols-2 gap-4">
            <FormField label="Tên đăng nhập" error={errors.username?.message} required>
              <Input id="create-username" placeholder="Ví dụ: john.doe" {...register('username')} />
            </FormField>
            <FormField label="Họ và tên" error={errors.fullName?.message} required>
              <Input id="create-fullname" placeholder="Nguyễn Văn A" {...register('fullName')} />
            </FormField>
          </div>

          <FormField label="Email" error={errors.email?.message} required>
            <Input id="create-email" type="email" placeholder="email@example.com" {...register('email')} />
          </FormField>

          <div className="grid grid-cols-2 gap-4">
            <FormField label="Mật khẩu" error={errors.password?.message} required>
              <Input id="create-password" type="password" placeholder="Tối thiểu 6 ký tự" {...register('password')} />
            </FormField>
            <FormField label="Số điện thoại" error={errors.phone?.message}>
              <Input id="create-phone" type="tel" placeholder="0912345678" {...register('phone')} />
            </FormField>
          </div>

          <FormField label="Vai trò" error={errors.role?.message} required>
            <Select id="create-role" {...register('role')}>
              <option value="STAFF">Nhân viên</option>
              <option value="MANAGER">Quản lý</option>
              <option value="ADMIN">Admin</option>
            </Select>
          </FormField>

          <DialogFooter className="mt-6 gap-2">
            <Button type="button" variant="outline" onClick={handleClose}>
              Hủy
            </Button>
            <Button type="submit" loading={isPending}>
              Tạo người dùng
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
