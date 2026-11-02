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
          <DialogTitle>Add New User</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-4 space-y-4" noValidate>
          <div className="grid grid-cols-2 gap-4">
            <FormField label="Username" error={errors.username?.message} required>
              <Input id="create-username" placeholder="e.g. john.doe" {...register('username')} />
            </FormField>
            <FormField label="Full Name" error={errors.fullName?.message} required>
              <Input id="create-fullname" placeholder="John Doe" {...register('fullName')} />
            </FormField>
          </div>

          <FormField label="Email" error={errors.email?.message} required>
            <Input id="create-email" type="email" placeholder="email@example.com" {...register('email')} />
          </FormField>

          <div className="grid grid-cols-2 gap-4">
            <FormField label="Password" error={errors.password?.message} required>
              <Input id="create-password" type="password" placeholder="Minimum 6 characters" {...register('password')} />
            </FormField>
            <FormField label="Phone Number" error={errors.phone?.message}>
              <Input id="create-phone" type="tel" placeholder="0912345678" {...register('phone')} />
            </FormField>
          </div>

          <FormField label="Role" error={errors.role?.message} required>
            <Select id="create-role" {...register('role')}>
              <option value="STAFF">Dispatcher (Staff)</option>
              <option value="MANAGER">Fleet Manager</option>
              <option value="ADMIN">Admin</option>
              <option value="CUSTOMER">Customer</option>
            </Select>
          </FormField>

          <DialogFooter className="mt-6 gap-2">
            <Button type="button" variant="outline" onClick={handleClose}>
              Cancel
            </Button>
            <Button type="submit" loading={isPending}>
              Create User
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
